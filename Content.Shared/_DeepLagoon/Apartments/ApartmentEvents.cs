using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Apartments;

[RegisterComponent]
public sealed partial class ApartmentGridComponent : Component;

[RegisterComponent]
public sealed partial class ApartmentTerminalComponent : Component;

[RegisterComponent]
public sealed partial class ApartmentResidentComponent : Component;

[Serializable, NetSerializable]
public sealed class ApartmentEditorEvent(NetEntity grid, string template, int revision, Guid token,
    List<ApartmentPlacement> layout, Dictionary<string, NetEntity> entities) : EntityEventArgs
{
    public readonly NetEntity Grid = grid;
    public readonly string Template = template;
    public readonly int Revision = revision;
    public readonly Guid Token = token;
    public readonly List<ApartmentPlacement> Layout = layout;
    public readonly Dictionary<string, NetEntity> Entities = entities;
}

[Serializable, NetSerializable]
public sealed class ApartmentApplyEvent(Guid token, int revision, List<ApartmentPlacement> layout) : EntityEventArgs
{
    public readonly Guid Token = token;
    public readonly int Revision = revision;
    public readonly List<ApartmentPlacement> Layout = layout;
}

[Serializable, NetSerializable]
public sealed class ApartmentActionEvent(string action) : EntityEventArgs
{
    public readonly string Action = action;
}

[Serializable, NetSerializable]
public sealed class ApartmentResultEvent(bool success, string message, int revision) : EntityEventArgs
{
    public readonly bool Success = success;
    public readonly string Message = message;
    public readonly int Revision = revision;
}
