using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.DiscordLink;

/// <summary>Server checks the linked Discord identity; client receives only its own status.</summary>
public abstract class SharedDiscordBoostSystem : EntitySystem
{
    public abstract bool HasActiveBoost(ICommonSession? session);
    public abstract int GetBoostyTier(ICommonSession? session);
    public abstract int[] RoleColors { get; }

    public bool HasDiscordRewardAccess(ICommonSession? session)
        => HasActiveBoost(session) || GetBoostyTier(session) > 0;

    public bool CanUseDonorItem(DonorCategory category, int tier, ICommonSession? session)
        => category switch
        {
            DonorCategory.None => true,
            DonorCategory.DiscordBoost => HasDiscordRewardAccess(session),
            DonorCategory.Boosty => tier is >= 1 and <= 3 && GetBoostyTier(session) >= tier,
            _ => false,
        };

    public Color GetDonorColor(DonorCategory category, int tier)
    {
        var index = category == DonorCategory.DiscordBoost ? 0 : Math.Clamp(tier, 1, 3);
        var rgb = RoleColors[index];
        if (rgb == 0)
            rgb = DefaultRoleColors[index];
        return Color.FromHex($"#{rgb:X6}");
    }

    // Discord's default color is 0, meaning no assigned color, not black.
    public static readonly int[] DefaultRoleColors = { 0xF47FFF, 0x5DADE2, 0xA780DE, 0xE6B450 };
}

public enum DonorCategory : byte { None, DiscordBoost, Boosty }

[Serializable, NetSerializable]
public sealed class DiscordBoostStatusEvent(int remainingSeconds, int boostyTier = 0, int boostyRemainingSeconds = 0, int[]? roleColors = null) : EntityEventArgs
{
    public int RemainingSeconds = remainingSeconds;
    public int BoostyTier = boostyTier;
    public int BoostyRemainingSeconds = boostyRemainingSeconds;
    public int[] RoleColors = roleColors ?? SharedDiscordBoostSystem.DefaultRoleColors;
}
