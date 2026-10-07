using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.DiscordLink;

/// <summary>Server checks the linked Discord identity; client receives only its own status.</summary>
public abstract class SharedDiscordBoostSystem : EntitySystem
{
    public abstract bool HasActiveBoost(ICommonSession? session);
}

[Serializable, NetSerializable]
public sealed class DiscordBoostStatusEvent(int remainingSeconds) : EntityEventArgs
{
    public int RemainingSeconds = remainingSeconds;
}
