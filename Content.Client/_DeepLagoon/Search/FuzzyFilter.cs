using System.Linq;

namespace Content.Client._DeepLagoon.Search;

/// <summary>
/// A yes/no filter for lists that test one item at a time (a delegate per item, no ordering).
/// It keeps the prepared query between calls and decides once per query whether typo matches are shown.
/// Give it the names of the whole list with <see cref="SetCorpus"/>; without it typos are always allowed.
/// </summary>
public sealed class FuzzyFilter
{
    private List<PreparedText>? _corpus;
    private string? _filter;
    private PreparedQuery _query = FuzzySearch.Prepare(null);
    private bool _allowFuzzy = true;

    /// <summary>The names of every item of the list. Call it whenever the list is rebuilt.</summary>
    public void SetCorpus(IEnumerable<string?> names)
    {
        _corpus = names.Select(FuzzySearch.Text).ToList();
        _filter = null;
    }

    public bool Test(string? filter, string? text)
    {
        if (string.IsNullOrEmpty(filter))
            return true;

        if (filter != _filter)
        {
            _filter = filter;
            _query = FuzzySearch.Prepare(filter);
            _allowFuzzy = _query.CanFuzzy && PlainMatches() < FuzzySearch.FuzzyBelowCount;
        }

        return FuzzySearch.Match(_query, FuzzySearch.Text(text), _allowFuzzy) != null;
    }

    private int PlainMatches()
    {
        if (_corpus == null)
            return 0;

        var count = 0;
        foreach (var text in _corpus)
        {
            if (FuzzySearch.Match(_query, text, false) != null)
                count++;
        }

        return count;
    }
}
