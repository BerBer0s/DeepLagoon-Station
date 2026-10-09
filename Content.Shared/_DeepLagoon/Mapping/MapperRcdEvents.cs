using Content.Shared.Atmos.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Mapping;

[Serializable, NetSerializable]
public enum MapperRcdUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MapperRcdSelectMessage : BoundUserInterfaceMessage
{
    public readonly string EntryId;

    public MapperRcdSelectMessage(string entryId)
    {
        EntryId = entryId;
    }
}

/// <summary>
/// A batch of tiles of one brush stroke. Cells are grid indices of <see cref="Grid"/> and are applied
/// strictly in the order they are listed.
/// </summary>
[Serializable, NetSerializable]
public sealed class MapperRcdStrokeEvent : EntityEventArgs
{
    public const int MaxCells = 64;

    public readonly NetEntity Tool;
    public readonly NetEntity Grid;
    public readonly string EntryId;
    public readonly Direction Direction;

    /// <summary>
    /// Pipe layer chosen when the stroke started. Only used by pipe entries.
    /// </summary>
    public readonly AtmosPipeLayer PipeLayer;

    public readonly List<Vector2i> Cells;

    public MapperRcdStrokeEvent(NetEntity tool, NetEntity grid, string entryId, Direction direction, AtmosPipeLayer pipeLayer, List<Vector2i> cells)
    {
        PipeLayer = pipeLayer;
        Tool = tool;
        Grid = grid;
        EntryId = entryId;
        Direction = direction;
        Cells = cells;
    }
}
