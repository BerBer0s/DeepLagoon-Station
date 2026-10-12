using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Tattoos;

[RegisterComponent]
public sealed partial class TattooMachineComponent : Component
{
    public EntityUid? Target;
    public EntityUid? Artist;
}

[Serializable, NetSerializable]
public enum TattooMachineUiKey : byte { Key }

[Serializable, NetSerializable]
public sealed class TattooMachineState(NetEntity target, List<Tattoo> tattoos, string status = "") : BoundUserInterfaceState
{
    public NetEntity Target = target;
    public List<Tattoo> Tattoos = tattoos;
    public string Status = status;
    public int Revision;
}

[Serializable, NetSerializable]
public sealed class TattooMachineApplyMessage(List<Tattoo> tattoos, int revision) : BoundUserInterfaceMessage
{
    public List<Tattoo> Tattoos = tattoos;
    public int Revision = revision;
}
