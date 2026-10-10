using Robust.Client.DeepLagoon;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._DeepLagoon.Search;

/// <summary>
/// Gives the engine admin spawn windows (entities, tiles) our fuzzy search. The hook type is added to
/// Robust.Client at build time by MSBuild/DeepLagoon.Search.targets, together with the patches that call it.
/// </summary>
public sealed class FuzzySearchHookController : UIController, IDeepLagoonSearch
{
    public override void Initialize()
    {
        DeepLagoonSearch.Impl = this;
    }

    public List<T> Rank<T>(IEnumerable<T> items, string query, Func<T, string?> name, Func<T, string?>? extra)
    {
        return FuzzySearch.Rank(items, query, name, extra);
    }
}
