using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Admin;

/// <summary>
/// Request from the client for one speed step of its admin ghost. Carries no numbers: the server decides
/// who may change what and computes the new multiplier itself.
/// </summary>
[Serializable, NetSerializable]
public sealed class AGhostSpeedStepEvent : EntityEventArgs
{
    public readonly bool Faster;

    public AGhostSpeedStepEvent(bool faster)
    {
        Faster = faster;
    }
}
