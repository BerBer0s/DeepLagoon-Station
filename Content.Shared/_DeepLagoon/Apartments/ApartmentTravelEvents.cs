using Content.Shared.DoAfter;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;

namespace Content.Shared._DeepLagoon.Apartments;

/// <summary>Mapped on the approved residential/CentCom stop, never inside a private apartment.</summary>
[RegisterComponent]
public sealed partial class ApartmentTransitStopComponent : Component;

/// <summary>Ordinary airlock on a residential stop; docking ports retain their normal behavior.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ApartmentEntranceComponent : Component
{
    [DataField] public bool AddedNoDeconstruct;
}

public sealed class ApartmentEntranceActivatedEvent(EntityUid user) : EntityEventArgs
{
    public readonly EntityUid User = user;
}

[RegisterComponent]
public sealed partial class ApartmentTripPendingComponent : Component;

[Serializable, NetSerializable]
public sealed class ApartmentTravelPerson(Guid user, string name, bool invited = false, bool inside = false, int seconds = 0)
{
    public readonly Guid User = user;
    public readonly string Name = name;
    public readonly bool Invited = invited, Inside = inside;
    public readonly int Seconds = seconds;
}

[Serializable, NetSerializable]
public sealed class ApartmentTravelStateEvent(Guid token, bool atHome, bool canManage, bool entryOpen,
    bool canDepart, bool pending, List<ApartmentTravelPerson> people, List<ApartmentTravelPerson> invitations,
    string message = "") : EntityEventArgs
{
    public readonly Guid Token = token;
    public readonly bool AtHome = atHome, CanManage = canManage, EntryOpen = entryOpen, CanDepart = canDepart, Pending = pending;
    public readonly List<ApartmentTravelPerson> People = people, Invitations = invitations;
    public readonly string Message = message;
}

public enum ApartmentTravelAction : byte { Close, Refresh, Depart, Visit, Invite, Revoke, CloseEntry, OpenEntry, Kick, Return, Edit }

[Serializable, NetSerializable]
public sealed class ApartmentTravelActionEvent(Guid token, ApartmentTravelAction action, Guid target = default) : EntityEventArgs
{
    public readonly Guid Token = token, Target = target;
    public readonly ApartmentTravelAction Action = action;
}

[Serializable, NetSerializable]
public sealed class ApartmentTravelClosedEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed partial class ApartmentTripDoAfterEvent : DoAfterEvent
{
    public Guid Ticket;
    public override DoAfterEvent Clone() => this;
}

/// <summary>Local transport hook. The HOST command may still use immediate Leave for recovery.</summary>
public sealed class ApartmentLeaveRequestedEvent(NetUserId user) : EntityEventArgs
{
    public readonly NetUserId User = user;
    public bool Handled;
    public string Message = "";
}
