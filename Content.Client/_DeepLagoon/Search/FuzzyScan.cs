namespace Content.Client._DeepLagoon.Search;

/// <summary>
/// One search over a list that can be advanced a few items at a time. <see cref="FuzzySearch.Rank{T}"/> runs it
/// in one go; <see cref="FuzzyBatchSearch{T}"/> spreads it over frames. First every item is matched without
/// typos; only if that finds fewer than <see cref="FuzzySearch.FuzzyBelowCount"/> items, a second pass over the
/// rest allows typos.
/// </summary>
public sealed class FuzzyScan<T>
{
    private struct Found
    {
        public T Item;
        public int Rank;
        public int Index;
    }

    private readonly IReadOnlyList<T> _items;
    private readonly PreparedQuery _query;
    private readonly Func<T, string?> _name;
    private readonly Func<T, string?>? _extra;
    private readonly Func<T, string?>? _extra2;
    private readonly bool[] _plain;
    private readonly List<Found> _matched = new();
    private int _position;
    private bool _fuzzyPass;

    public bool Done { get; private set; }

    public FuzzyScan(
        IReadOnlyList<T> items,
        PreparedQuery query,
        Func<T, string?> name,
        Func<T, string?>? extra,
        Func<T, string?>? extra2)
    {
        _items = items;
        _query = query;
        _name = name;
        _extra = extra;
        _extra2 = extra2;
        _plain = new bool[items.Count];
    }

    /// <summary>Matches up to this many more items.</summary>
    public void Step(int maxItems)
    {
        while (!Done && maxItems > 0)
        {
            if (_position >= _items.Count)
            {
                if (!_fuzzyPass && _matched.Count < FuzzySearch.FuzzyBelowCount && _query.CanFuzzy)
                {
                    _fuzzyPass = true;
                    _position = 0;
                    continue;
                }

                Done = true;
                break;
            }

            var index = _position++;
            maxItems--;
            var item = _items[index];
            if (_fuzzyPass)
            {
                if (_plain[index])
                    continue;

                var fuzzyRank = FuzzySearch.Match(_query, _name(item));
                if (fuzzyRank != null)
                    _matched.Add(new Found { Item = item, Rank = fuzzyRank.Value, Index = index });

                continue;
            }

            var rank = FuzzySearch.Match(_query, _name(item), false);
            if (rank == null && _extra != null && FuzzySearch.Match(_query, _extra(item), false) != null)
                rank = FuzzySearch.MakeRank(FuzzySearch.TierExtra, 0);
            if (rank == null && _extra2 != null && FuzzySearch.Match(_query, _extra2(item), false) != null)
                rank = FuzzySearch.MakeRank(FuzzySearch.TierExtra, 0);

            if (rank == null)
                continue;

            _plain[index] = true;
            _matched.Add(new Found { Item = item, Rank = rank.Value, Index = index });
        }
    }

    /// <summary>The matching items, best first; equal ranks keep their incoming order.</summary>
    public List<T> Ranked()
    {
        _matched.Sort(CompareByRank);
        return Items();
    }

    /// <summary>The matching items in their incoming order.</summary>
    public List<T> InOrder()
    {
        _matched.Sort(CompareByIndex);
        return Items();
    }

    private List<T> Items()
    {
        var result = new List<T>(_matched.Count);
        foreach (var found in _matched)
        {
            result.Add(found.Item);
        }

        return result;
    }

    private static int CompareByRank(Found a, Found b)
    {
        return a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Index.CompareTo(b.Index);
    }

    private static int CompareByIndex(Found a, Found b)
    {
        return a.Index.CompareTo(b.Index);
    }
}
