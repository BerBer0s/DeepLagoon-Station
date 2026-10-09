using Content.Server.GameTicking;
using Content.Shared._DeepLagoon.Admin;
using Content.Shared.Administration;
using Content.Shared.Administration.Managers;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._DeepLagoon.Admin;

/// <summary>
/// Applies the Shift + wheel speed steps of the admin ghost. The request carries only a direction.
/// The server checks that the sender is a real admin and is attached to an <c>AdminObserver</c>,
/// computes the new multiplier from the component limits, and applies it as a movement speed modifier.
/// </summary>
public sealed partial class AGhostSpeedSystem : SharedAGhostSpeedSystem
{
    [Dependency] private ISharedAdminManager _admin = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// The popup waits this long after the last notch, so scrolling does not stack popups.
    /// </summary>
    private static readonly TimeSpan PopupDelay = TimeSpan.FromMilliseconds(350);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AGhostSpeedStepEvent>(OnStep);
    }

    private void OnStep(AGhostSpeedStepEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid ||
            !TryComp(uid, out AGhostSpeedComponent? comp) ||
            MetaData(uid).EntityPrototype?.ID != GameTicker.AdminObserverPrototypeName ||
            !_admin.HasAdminFlag(args.SenderSession, AdminFlags.Admin))
        {
            return;
        }

        var factor = ev.Faster ? comp.Step : 1f / comp.Step;
        var multiplier = Math.Clamp(comp.Multiplier * factor, comp.Min, comp.Max);

        // Walking back down to 1 would otherwise stop a hair off it from float error.
        if (MathF.Abs(multiplier - 1f) < 0.001f)
            multiplier = 1f;

        comp.PopupAt = _timing.CurTime + PopupDelay;

        if (MathHelper.CloseTo(multiplier, comp.Multiplier))
            return;

        comp.Multiplier = multiplier;
        Dirty(uid, comp);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AGhostSpeedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PopupAt is not { } at || _timing.CurTime < at)
                continue;

            comp.PopupAt = null;
            _popup.PopupEntity(Loc.GetString("dl-aghost-speed", ("speed", $"{comp.Multiplier:0.##}")), uid, uid);
        }
    }
}
