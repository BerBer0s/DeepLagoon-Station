using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Mapping;

/// <summary>Grid-local coordinates; entity centers and decal origins retain sub-tile precision.</summary>
[Serializable, NetSerializable]
public sealed class GridEditorItem
{
    public string Id = "";
    public string Kind = "";
    public string Prototype = "";
    public float X;
    public float Y;
    public float Rotation;
    public string Color = "#FFFFFFFF";
    public int Z;
    public bool Cleanable;
    public bool Anchored;
    public byte Variant;
    public byte Flags;
    public byte Mirror;
    public bool Same(GridEditorItem other) => Id == other.Id && Kind == other.Kind && Prototype == other.Prototype &&
        X == other.X && Y == other.Y && Rotation == other.Rotation && Color == other.Color && Z == other.Z &&
        Cleanable == other.Cleanable && Anchored == other.Anchored && Variant == other.Variant && Flags == other.Flags && Mirror == other.Mirror;
}

[Serializable, NetSerializable]
public sealed class GridEditorStateEvent(NetEntity grid, Guid token, int x, int y, int width, int height,
    List<GridEditorItem> items, bool initialized = false) : EntityEventArgs
{
    public readonly NetEntity Grid = grid;
    public readonly Guid Token = token;
    public readonly int X = x, Y = y, Width = width, Height = height;
    public readonly List<GridEditorItem> Items = items;
    public readonly bool Initialized = initialized;
}

[Serializable, NetSerializable]
public sealed class GridEditorApplyEvent(Guid token, List<GridEditorItem> items) : EntityEventArgs
{
    public readonly Guid Token = token;
    public readonly List<GridEditorItem> Items = items;
}

[Serializable, NetSerializable]
public sealed class GridEditorResultEvent(string message, bool success) : EntityEventArgs
{
    public readonly string Message = message;
    public readonly bool Success = success;
}

[Serializable, NetSerializable]
public sealed class GridEditorCloseEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class GridEditorRegionEvent(Guid token, int x, int y) : EntityEventArgs
{
    public readonly Guid Token = token;
    public readonly int X = x, Y = y;
}

[Serializable, NetSerializable]
public sealed class GridEditorDownloadEvent(Guid token, Guid request, bool wholeMap) : EntityEventArgs
{
    public readonly Guid Token = token, Request = request;
    public readonly bool WholeMap = wholeMap;
}

[Serializable, NetSerializable]
public sealed class GridEditorFileEvent(Guid request, int index, int count, byte[] bytes, string error = "") : EntityEventArgs
{
    public readonly Guid Request = request;
    public readonly int Index = index, Count = count;
    public readonly byte[] Bytes = bytes;
    public readonly string Error = error;
}
