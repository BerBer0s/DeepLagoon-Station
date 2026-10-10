using System.Linq;
using System.Text;

namespace Content.Client._DeepLagoon.Search;

/// <summary>A query, normalized and split into words. Build it once per text change with <see cref="FuzzySearch.Prepare"/>.</summary>
public sealed class PreparedQuery
{
    public readonly string Normalized;

    /// <summary>Words of the query, split on whitespace. They match in any order.</summary>
    public readonly string[] Words;

    public bool IsEmpty => Normalized.Length == 0;

    /// <summary>True when at least one word is long enough for typo tolerance.</summary>
    public readonly bool CanFuzzy;

    internal PreparedQuery(string normalized, string[] words)
    {
        Normalized = normalized;
        Words = words;
        CanFuzzy = words.Any(FuzzySearch.CanFuzzyWord);
    }
}

/// <summary>A searched string, normalized and split into words once. Get it from <see cref="FuzzySearch.Text"/>.</summary>
public sealed class PreparedText
{
    public readonly string Normalized;

    /// <summary>Runs of letters and digits, so "(wall)" gives "wall".</summary>
    public readonly string[] Words;

    internal PreparedText(string normalized, string[] words)
    {
        Normalized = normalized;
        Words = words;
    }
}

/// <summary>
/// Case-insensitive search that forgives typos. There is a TypeScript twin in
/// TGUI/packages/tgui/interfaces/fuzzySearch.ts; the algorithm, tiers and constants must stay equal in both.
/// </summary>
/// <remarks>
/// Normalization: lower case (invariant), "ё" is "е", whitespace runs collapse to one space.
/// Every word of the query has to be found in the text, in any order.
///
/// Tiers, best first (the rank is tier * 16 + typo cost, lower is better):
/// exact (whole string), prefix (string starts with the query), word prefix, substring,
/// the extra text (description), typos.
///
/// Typos: only for query words of 4+ characters without digits, against text words of 4+ characters. One mistake is
/// allowed for 4-6 characters, two for 7+. A mistake is an insert, a delete, a replace or a swap of neighbours
/// (optimal string alignment distance). A word is also compared with the same-length start of a longer text
/// word, so half-typed input still works. Typo matches are shown only when there are fewer than
/// <see cref="FuzzyBelowCount"/> better ones.
///
/// Examples: "wsll" finds "Wall" (typo); "wall" finds "Wall"; "стена" finds "Стена";
/// "елка" finds "ёлка"; "wal" finds "Wall" (prefix); "all" finds "Wall" (substring);
/// "wl" does not find "Wall" (too short for typos); "wsll" does not find "Walls and doors" when
/// there are already five plain matches.
/// </remarks>
public static class FuzzySearch
{
    /// <summary>Typos start at this length of a query word and of a text word.</summary>
    public const int MinFuzzyWordLength = 4;

    /// <summary>Query words of this length and longer may have two mistakes instead of one.</summary>
    public const int LongWordLength = 7;

    /// <summary>Typo matches are shown only when there are fewer plain matches than this.</summary>
    public const int FuzzyBelowCount = 5;

    public const int TierExact = 0;
    public const int TierPrefix = 1;
    public const int TierWordPrefix = 2;
    public const int TierSubstring = 3;
    public const int TierExtra = 4;
    public const int TierFuzzy = 5;

    private const int TierMultiplier = 16;
    private const int MaxCost = TierMultiplier - 1;
    private const int TextCacheLimit = 32768;

    private static readonly Dictionary<string, PreparedText> TextCache = new();

    // Scratch rows of the distance calculation. The UI runs on one thread.
    private static int[] _rowA = new int[32];
    private static int[] _rowB = new int[32];
    private static int[] _rowC = new int[32];

    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var raw in text)
        {
            if (char.IsWhiteSpace(raw))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            var c = char.ToLowerInvariant(raw);
            builder.Append(c == 'ё' ? 'е' : c);
        }

        return builder.ToString();
    }

    public static PreparedQuery Prepare(string? query)
    {
        var normalized = Normalize(query);
        if (normalized.Length == 0)
            return new PreparedQuery(string.Empty, new string[0]);

        var words = new List<string>();
        var start = 0;
        for (var i = 0; i <= normalized.Length; i++)
        {
            if (i < normalized.Length && normalized[i] != ' ')
                continue;
            words.Add(normalized.Substring(start, i - start));
            start = i + 1;
        }

        return new PreparedQuery(normalized, words.ToArray());
    }

    /// <summary>The prepared form of a text. Cached, so repeated calls for the same string are cheap.</summary>
    public static PreparedText Text(string? text)
    {
        text ??= string.Empty;
        if (TextCache.TryGetValue(text, out var cached))
            return cached;

        var normalized = Normalize(text);
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= normalized.Length; i++)
        {
            if (i < normalized.Length && char.IsLetterOrDigit(normalized[i]))
            {
                if (start < 0)
                    start = i;
                continue;
            }

            if (start >= 0)
                words.Add(normalized.Substring(start, i - start));
            start = -1;
        }

        if (TextCache.Count >= TextCacheLimit)
            TextCache.Clear();

        var prepared = new PreparedText(normalized, words.ToArray());
        TextCache[text] = prepared;
        return prepared;
    }

    /// <summary>
    /// The rank of the text for the query (lower is better), or null when it does not match.
    /// An empty query matches everything with rank 0.
    /// </summary>
    public static int? Match(PreparedQuery query, PreparedText text, bool allowFuzzy = true)
    {
        if (query.IsEmpty)
            return 0;

        if (text.Normalized == query.Normalized)
            return TierExact * TierMultiplier;

        if (text.Normalized.StartsWith(query.Normalized, StringComparison.Ordinal))
            return TierPrefix * TierMultiplier;

        var tier = TierWordPrefix;
        var cost = 0;
        foreach (var word in query.Words)
        {
            var wordTier = MatchWord(word, text, allowFuzzy, out var wordCost);
            if (wordTier < 0)
                return null;

            tier = Math.Max(tier, wordTier);
            cost += wordCost;
        }

        return MakeRank(tier, cost);
    }

    /// <summary>Same as <see cref="Match(PreparedQuery,PreparedText,bool)"/> for a plain string.</summary>
    public static int? Match(PreparedQuery query, string? text, bool allowFuzzy = true)
    {
        return Match(query, Text(text), allowFuzzy);
    }

    /// <summary>
    /// Keeps the items that match the query and orders them best first; items of one rank keep their
    /// incoming order. An empty query returns the items unchanged, in the same order.
    /// </summary>
    /// <param name="name">The main text of an item; it may also match with typos.</param>
    /// <param name="extra">An additional text (a description); it matches only as a substring, without typos.</param>
    public static List<T> Rank<T>(
        IEnumerable<T> items,
        string? query,
        Func<T, string?> name,
        Func<T, string?>? extra = null)
    {
        var matched = Search(items, query, name, extra);
        if (matched == null)
            return items.ToList();

        return matched.OrderBy(pair => pair.Rank).Select(pair => pair.Item).ToList();
    }

    /// <summary>
    /// Same matching as <see cref="Rank{T}"/>, but the matching items keep their incoming order
    /// (for lists that have their own order and only need a filter).
    /// </summary>
    public static List<T> Filter<T>(
        IEnumerable<T> items,
        string? query,
        Func<T, string?> name,
        Func<T, string?>? extra = null)
    {
        var matched = Search(items, query, name, extra);
        if (matched == null)
            return items.ToList();

        return matched.OrderBy(pair => pair.Index).Select(pair => pair.Item).ToList();
    }

    // Null for an empty query: everything matches.
    private static List<(T Item, int Rank, int Index)>? Search<T>(
        IEnumerable<T> items,
        string? query,
        Func<T, string?> name,
        Func<T, string?>? extra)
    {
        var prepared = Prepare(query);
        if (prepared.IsEmpty)
            return null;

        var matched = new List<(T Item, int Rank, int Index)>();
        var rest = new List<(T Item, int Index)>();
        var index = 0;
        foreach (var item in items)
        {
            var rank = Match(prepared, name(item), false);
            if (rank == null && extra != null && Match(prepared, extra(item), false) != null)
                rank = MakeRank(TierExtra, 0);

            if (rank != null)
                matched.Add((item, rank.Value, index));
            else
                rest.Add((item, index));

            index++;
        }

        if (matched.Count < FuzzyBelowCount && prepared.CanFuzzy)
        {
            foreach (var (item, itemIndex) in rest)
            {
                var rank = Match(prepared, name(item));
                if (rank != null)
                    matched.Add((item, rank.Value, itemIndex));
            }
        }

        return matched;
    }

    /// <summary>
    /// Plain substring search that ignores case and "ё" but forgives nothing; for names of people and other
    /// texts where a typo match would show the wrong one. An empty query matches everything.
    /// </summary>
    public static bool Contains(string? text, string? query)
    {
        var normalizedQuery = Normalize(query);
        return normalizedQuery.Length == 0
               || Text(text).Normalized.Contains(normalizedQuery, StringComparison.Ordinal);
    }

    /// <summary>A query word may have typos when it is long enough and has no digits (numbers and codes are exact).</summary>
    public static bool CanFuzzyWord(string word)
    {
        if (word.Length < MinFuzzyWordLength)
            return false;

        foreach (var c in word)
        {
            if (char.IsDigit(c))
                return false;
        }

        return true;
    }

    /// <summary>How many mistakes a query word of this length may have.</summary>
    public static int Tolerance(int wordLength)
    {
        return wordLength >= LongWordLength ? 2 : 1;
    }

    private static int MakeRank(int tier, int cost)
    {
        return tier * TierMultiplier + Math.Min(cost, MaxCost);
    }

    // The tier a single query word reaches in the text, or -1.
    private static int MatchWord(string word, PreparedText text, bool allowFuzzy, out int cost)
    {
        cost = 0;
        foreach (var candidate in text.Words)
        {
            if (candidate.StartsWith(word, StringComparison.Ordinal))
                return TierWordPrefix;
        }

        if (text.Normalized.Contains(word, StringComparison.Ordinal))
            return TierSubstring;

        if (!allowFuzzy || !CanFuzzyWord(word))
            return -1;

        var tolerance = Tolerance(word.Length);
        var best = tolerance + 1;
        foreach (var candidate in text.Words)
        {
            if (candidate.Length < MinFuzzyWordLength)
                continue;

            if (Math.Abs(candidate.Length - word.Length) <= tolerance)
                best = Math.Min(best, Distance(word, candidate, word.Length, candidate.Length, Math.Min(best - 1, tolerance)));

            // Half-typed input: compare with the start of a longer word.
            if (candidate.Length > word.Length)
                best = Math.Min(best, Distance(word, candidate, word.Length, word.Length, Math.Min(best - 1, tolerance)));
        }

        if (best > tolerance)
            return -1;

        cost = best;
        return TierFuzzy;
    }

    // Optimal string alignment distance of a[0..lengthA) and b[0..lengthB) (insert, delete, replace, swap of
    // neighbours). Gives up above max and returns max + 1.
    private static int Distance(string a, string b, int lengthA, int lengthB, int max)
    {
        if (Math.Abs(lengthA - lengthB) > max)
            return max + 1;

        if (_rowA.Length <= lengthB)
        {
            _rowA = new int[lengthB + 1];
            _rowB = new int[lengthB + 1];
            _rowC = new int[lengthB + 1];
        }

        // previous2 is row i-2, previous is row i-1, current is row i.
        var previous2 = _rowA;
        var previous = _rowB;
        var current = _rowC;
        for (var j = 0; j <= lengthB; j++)
        {
            previous[j] = j;
        }

        var previousMin = 0;
        for (var i = 1; i <= lengthA; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= lengthB; j++)
            {
                var substitution = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + substitution);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    value = Math.Min(value, previous2[j - 2] + 1);

                current[j] = value;
                rowMin = Math.Min(rowMin, value);
            }

            // A swap reaches back two rows, so both of the last two rows have to be over the limit.
            if (rowMin > max && previousMin > max)
                return max + 1;

            previousMin = rowMin;

            var next = previous2;
            previous2 = previous;
            previous = current;
            current = next;
        }

        return previous[lengthB];
    }
}
