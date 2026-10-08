using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._Goobstation.Research;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

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
    ];

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

    /// <summary>Icons for the requested technologies, as a payload the page merges into its icon store.</summary>
    public string BuildIcons(IEnumerable<string> technologyIds)
    {
        var icons = technologyIds.Select(id => new TguiData()
            .String("id", id)
            .Array("layers", Icon(_prototypes.Index<TechnologyPrototype>(id))));
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
            .Array("prerequisites", tech.TechnologyPrerequisites.Select(id => new TguiData().String("id", id)))
            .Array("recipes", tech.RecipeUnlocks.Select(id => new TguiData().String("name", _lathe.GetRecipeName(id))));
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
