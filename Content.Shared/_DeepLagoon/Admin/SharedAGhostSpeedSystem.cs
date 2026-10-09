using Content.Shared.Movement.Systems;

namespace Content.Shared._DeepLagoon.Admin;

public abstract partial class SharedAGhostSpeedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Only the server refreshes the modifiers; the client receives the result with the
        // MovementSpeedModifierComponent state and predicts movement from it.
        SubscribeLocalEvent<AGhostSpeedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(EntityUid uid, AGhostSpeedComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.Multiplier);
    }
}
