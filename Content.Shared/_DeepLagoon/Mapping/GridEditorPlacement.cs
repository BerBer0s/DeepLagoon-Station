using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._DeepLagoon.Mapping;

public static class GridEditorPlacement
{
    public const string DiagonalFloor = "DLPlatingDiagonal";
    public static bool HasTag(EntityPrototype prototype, string tag, TagSystem system) =>
        prototype.TryGetComponent<TagComponent>(out var tags) && system.HasTag(tags, tag);
    public static bool IsWall(EntityPrototype prototype, TagSystem system) => HasTag(prototype, "Wall", system);
    public static bool IsDiagonalWall(EntityPrototype prototype, TagSystem system) => IsWall(prototype, system) && HasTag(prototype, "Diagonal", system);
    public static bool IsExclusive(EntityPrototype prototype, TagSystem system) => IsWall(prototype, system) || HasTag(prototype, "Window", system) ||
        prototype.Components.ContainsKey("EntityStorage") ||
        prototype.Components.ContainsKey("PlaceableSurface") && prototype.Components.ContainsKey("Climbable");
}
