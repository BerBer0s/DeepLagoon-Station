using System.Linq;

namespace Content.Shared._DeepLagoon.Apartments;

/// <summary>Round-local, bounded invitations. Existing occupants do not depend on entry permissions.</summary>
public sealed class ApartmentGuestAccess
{
    public const int MaxInvitations = 32;
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromMinutes(5);
    public bool Open { get; private set; } = true;
    public readonly Dictionary<Guid, TimeSpan> Invitations = new();

    public bool Invite(Guid guest, TimeSpan now)
    {
        Prune(now);
        if (!Open || guest == Guid.Empty || (!Invitations.ContainsKey(guest) && Invitations.Count >= MaxInvitations)) return false;
        Invitations[guest] = now + InvitationLifetime;
        return true;
    }

    public bool MayEnter(Guid guest, TimeSpan now) => Open && Invitations.TryGetValue(guest, out var expiry) && expiry > now;
    public void Revoke(Guid guest) => Invitations.Remove(guest);
    public void SetOpen(bool open) { Open = open; if (!open) Invitations.Clear(); }
    public void Prune(TimeSpan now)
    {
        foreach (var guest in Invitations.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) Invitations.Remove(guest);
    }
}
