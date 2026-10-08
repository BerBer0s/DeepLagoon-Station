using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._Goobstation.Research;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._DeepLagoon.Research;

/// <summary>
/// JSON for the TGUI research console. Static data (technologies, labels) is sent once per set of
/// visible technologies and cached for the whole session. Icons are not part of it: the page asks
/// for the icons of the technologies it is about to show, and each icon is decoded only once.
/// Dynamic data (points, availability) is sent on every state change.
/// </summary>
public sealed class ResearchConsoleTguiData
{
    private static readonly string[] LabelKeys =
    [
        "dl-research-servers",
        "dl-research-points",
        "dl-research-research",
        "dl-research-researched",
        "dl-research-locked",
        "dl-research-unaffordable",
        "dl-research-no-access",
        "dl-research-no-server",
        "dl-research-tier",
        "dl-research-cost",
        "dl-research-recenter",
        "dl-research-fit",
        "dl-research-zoom-in",
        "dl-research-zoom-out",
        "dl-research-confirm",
        "dl-research-hint-locked",
        "dl-research-details-requires",
        "dl-research-details-opens",
        "dl-research-details-recipes",
        "dl-research-details-effects",
        "dl-research-details-hub",
        "dl-research-details-outside",
        "dl-research-details-hide",
        "dl-research-details-show",
        "dl-research-tabs-all",
        "dl-research-search-placeholder",
        "dl-research-search-clear",
        "dl-research-search-empty",
        "dl-research-search-recipe",
        "dl-research-search-more",
    ];

    /// <summary>Marks an icon key of the page as a lathe recipe; any other key is a technology id.</summary>
    public const string RecipeIconPrefix = "recipe:";

    // Shared by every console window, so reopening the console does no image work.
    private static TguiSpriteImages _images = new();
    private static readonly Dictionary<string, string> StaticCache = new();

    private readonly IPrototypeManager _prototypes = IoCManager.Resolve<IPrototypeManager>();
    private readonly SharedLatheSystem _lathe;

    public ResearchConsoleTguiData(IEntityManager entities)
    {
        _lathe = entities.System<SharedLatheSystem>();
    }

    public static void ClearCache()
    {
        StaticCache.Clear();
        _images = new TguiSpriteImages();
    }

    public string BuildStatic(IReadOnlyCollection<string> technologyIds, string key)
    {
        key += '|' + IoCManager.Resolve<ILocalizationManager>().DefaultCulture?.Name;
        if (StaticCache.TryGetValue(key, out var cached))
            return cached;

        var technologies = technologyIds
            .Select(id => _prototypes.Index<TechnologyPrototype>(id))
            .OrderBy(tech => tech.Discipline.Id, StringComparer.Ordinal)
            .ThenBy(tech => tech.Tier)
            .ThenBy(tech => tech.ID, StringComparer.Ordinal)
            .ToList();

        var disciplines = technologies
            .Select(tech => tech.Discipline)
            .Distinct()
            .Select(id => _prototypes.Index(id))
            .Select(discipline => new TguiData()
                .String("id", discipline.ID)
                .String("name", Loc.GetString(discipline.Name))
                .String("shortName", discipline.UiName)
                .String("color", discipline.Color.ToHexNoAlpha())
                .String("icon", _images.Frame(discipline.Icon)));

        var labels = new TguiData();
        foreach (var labelKey in LabelKeys)
            labels.String(labelKey, Loc.GetString(labelKey));

        var data = new TguiData()
            .Object("labels", labels)
            .Array("disciplines", disciplines)
            .Array("techs", technologies.Select(BuildTechnology));
        return StaticCache[key] = "{\"static_data\":" + data + "}";
    }

    public string BuildDynamic(ResearchConsoleBoundInterfaceState state, bool hasAccess)
    {
        return new TguiData()
            .Number("points", state.Points)
            .Bool("hasAccess", hasAccess)
            .Array("states", state.Researches
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new TguiData().String("id", pair.Key).String("state", StateName(pair.Value))))
            .ToString();
    }

    /// <summary>Ids of the recipes the given technologies unlock.</summary>
    public HashSet<string> RecipeIds(IEnumerable<string> technologyIds)
    {
        return technologyIds
            .SelectMany(id => _prototypes.Index<TechnologyPrototype>(id).RecipeUnlocks)
            .Select(id => id.Id)
            .ToHashSet();
    }

    /// <summary>
    /// Icons for the requested keys (a technology id, or a recipe id behind <see cref="RecipeIconPrefix"/>),
    /// as a payload the page merges into its icon store.
    /// </summary>
    public string BuildIcons(IEnumerable<string> keys)
    {
        var icons = keys.Select(key => new TguiData()
            .String("id", key)
            .Array("layers", key.StartsWith(RecipeIconPrefix, StringComparison.Ordinal)
                ? RecipeIcon(_prototypes.Index<LatheRecipePrototype>(key[RecipeIconPrefix.Length..]))
                : Icon(_prototypes.Index<TechnologyPrototype>(key))));
        return "{\"data\":" + new TguiData().Object("iconBatch", new TguiData().Array("icons", icons)) + "}";
    }

    private TguiData BuildTechnology(TechnologyPrototype tech)
    {
        return new TguiData()
            .String("id", tech.ID)
            .String("name", Loc.GetString(tech.Name))
            .String("discipline", tech.Discipline)
            .Number("tier", tech.Tier)
            .Number("cost", tech.Cost)
            .Array("prerequisites", tech.TechnologyPrerequisites.Select(id => new TguiData()
                .String("id", id)
                .String("name", Loc.GetString(_prototypes.Index(id).Name))))
            .Array("recipes", tech.RecipeUnlocks.Select(id => new TguiData()
                .String("id", id)
                .String("name", _lathe.GetRecipeName(id))))
            .Array("effects", tech.GenericUnlocks.Select(unlock => new TguiData()
                .String("text", Loc.GetString(unlock.UnlockDescription))));
    }

    private IEnumerable<TguiData> RecipeIcon(LatheRecipePrototype recipe)
    {
        switch (recipe.Icon)
        {
            case SpriteSpecifier.EntityPrototype entity when _prototypes.HasIndex<EntityPrototype>(entity.EntityPrototypeId):
                return _images.Item(entity.EntityPrototypeId);
            case { } icon:
                return [new TguiData().String("url", _images.Frame(icon)).String("color", "#ffffff")];
        }

        if (recipe.Result is { } result && _prototypes.HasIndex<EntityPrototype>(result))
            return _images.Item(result);
        return [];
    }

    private IEnumerable<TguiData> Icon(TechnologyPrototype tech)
    {
        if (tech.EntityIcon is { } entityId && _prototypes.HasIndex<EntityPrototype>(entityId))
            return _images.Item(entityId);
        if (tech.Icon != null)
            return [new TguiData().String("url", _images.Frame(tech.Icon)).String("color", "#ffffff")];
        return [];
    }

    private static string StateName(ResearchAvailability availability) => availability switch
    {
        ResearchAvailability.Researched => "researched",
        ResearchAvailability.Available => "available",
        ResearchAvailability.PrereqsMet => "unaffordable",
        _ => "locked",
    };
}
