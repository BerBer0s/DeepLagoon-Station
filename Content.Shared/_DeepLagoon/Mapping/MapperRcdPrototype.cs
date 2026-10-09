using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._DeepLagoon.Mapping;

/// <summary>
/// A node of the mapper RCD radial menu. A category shows its child categories and its own entries.
/// Exactly one category without a parent is the root of the menu.
/// </summary>
[Prototype]
public sealed partial class MapperRcdCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localization key of the category name.
    /// </summary>
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField]
    public ProtoId<MapperRcdCategoryPrototype>? Parent;

    /// <summary>
    /// Icon of the button that opens this category. When null the icon of the first entry inside is used.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Sort key among sibling categories (ascending).
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// Kind of things this category (and everything below it) places. Entities of its entries are recognised
    /// as that kind when a filtered delete entry looks for something to remove.
    /// </summary>
    [DataField]
    public MapperRcdFilter Filter;

    [DataField]
    public List<MapperRcdEntry> Entries = new();
}

/// <summary>
/// One thing the brush can do: place a tile, place an entity, or remove a structure.
/// </summary>
[DataDefinition]
public sealed partial class MapperRcdEntry
{
    /// <summary>
    /// Unique id across the whole catalog. Defaults to <see cref="Prototype"/>.
    /// </summary>
    [DataField]
    public string? Id;

    [DataField(required: true)]
    public MapperRcdMode Mode;

    /// <summary>
    /// Tile definition id for <see cref="MapperRcdMode.Tile"/>, entity prototype id for
    /// <see cref="MapperRcdMode.Entity"/>. For <see cref="MapperRcdMode.Deconstruct"/> it is the
    /// entity used as the placement ghost.
    /// </summary>
    [DataField]
    public string? Prototype;

    /// <summary>
    /// Localization key. When null the name of the entity or tile is used.
    /// </summary>
    [DataField]
    public string? Name;

    /// <summary>
    /// Localization key of a short qualifier shown after the name, e.g. "diagonal". Tells apart entities that
    /// share a name with a sibling.
    /// </summary>
    [DataField]
    public string? Suffix;

    /// <summary>
    /// When null the entity prototype icon is used.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Which kind of occupant of a tile this entity replaces, see the replacement rule in the server system.
    /// </summary>
    [DataField]
    public MapperRcdSlot Slot = MapperRcdSlot.Structure;

    /// <summary>
    /// Whether the placement direction chosen by the user rotates the spawned entity.
    /// </summary>
    [DataField]
    public bool Rotatable;

    /// <summary>
    /// For <see cref="MapperRcdMode.Deconstruct"/>: what kind of thing the entry removes. Any removes the
    /// topmost structure.
    /// </summary>
    [DataField]
    public MapperRcdFilter Filter;

    public string EffectiveId => Id ?? Prototype ?? string.Empty;
}

public enum MapperRcdFilter : byte
{
    Any,
    Walls,
    Windows,
    Doors,
    Pipes,

    /// <summary>
    /// Catwalks first, then the tile itself: a covering floor is peeled down to plating, plating is removed last.
    /// </summary>
    Floors,
}

public enum MapperRcdMode : byte
{
    Tile,
    Entity,
    Deconstruct,
}

public enum MapperRcdSlot : byte
{
    /// <summary>
    /// Full-tile structure: walls, windows, airlocks, grilles, girders.
    /// </summary>
    Structure,

    /// <summary>
    /// Partial-tile structure on a tile edge: directional windows, railings.
    /// </summary>
    Edge,

    /// <summary>
    /// Atmos pipe or device. The pipe layer is taken from <c>AtmosPipeLayersComponent</c> of the prototype.
    /// </summary>
    Pipe,

    /// <summary>
    /// Grilles: coexists with windows (a window goes on top of a grille and a grille goes under a window),
    /// is replaced by another grille and by walls and doors, blocked by the same things as a structure.
    /// </summary>
    Under,

    /// <summary>
    /// Placed on top of whatever is there (catwalks, firelocks). Replaces nothing, is blocked by nothing;
    /// only an identical entity already on the tile makes the cell a no-op.
    /// </summary>
    Overlay,
}
