using Content.Shared.Mobs.Components;
using Content.Server.Actions;
using Content.Server.Humanoid;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs;
using Content.Shared.Toggleable;
using Content.Shared.Wagging;
using Robust.Shared.Prototypes;

namespace Content.Server.Wagging;

/// <summary>
/// Adds an action to toggle wagging animation for tails markings that supporting this
/// </summary>
public sealed partial class WaggingSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private HumanoidAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaggingComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WaggingComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WaggingComponent, ToggleActionEvent>(OnToggle);
        SubscribeLocalEvent<WaggingComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent args)
    {
        if (TryComp<HumanoidAppearanceComponent>(args.Mob, out var humanoid) && HasTail(humanoid))
            EnsureComp<WaggingComponent>(args.Mob);
    }

    private bool HasTail(HumanoidAppearanceComponent humanoid)
    {
        if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
            return false;
        foreach (var marking in markings)
        {
            if (_prototype.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto) && proto.BodyPart == HumanoidVisualLayers.Tail)
                return true;
        }
        return false;
    }

    private void OnStartup(EntityUid uid, WaggingComponent component, ComponentStartup args)
        => _actions.AddAction(uid, ref component.ActionEntity, component.Action, uid);

    private void OnShutdown(EntityUid uid, WaggingComponent component, ComponentShutdown args)
        => _actions.RemoveAction(uid, component.ActionEntity);

    private void OnToggle(EntityUid uid, WaggingComponent component, ref ToggleActionEvent args)
    {
        if (!args.Handled)
            args.Handled = TryToggleWagging(uid, wagging: component);
    }

    private void OnMobStateChanged(EntityUid uid, WaggingComponent component, MobStateChangedEvent args)
    {
        if (component.Wagging && args.NewMobState != MobState.Alive)
            TryToggleWagging(uid, wagging: component);
    }

    public bool TryToggleWagging(EntityUid uid, WaggingComponent? wagging = null, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref wagging, ref humanoid) || !HasTail(humanoid))
            return false;
        if (!wagging.Wagging && TryComp<MobStateComponent>(uid, out var mob) && mob.CurrentState != MobState.Alive)
            return false;

        wagging.Wagging = !wagging.Wagging;
        _actions.SetToggled(wagging.ActionEntity, wagging.Wagging);
        Dirty(uid, wagging);

        var markings = humanoid.MarkingSet.Markings[MarkingCategories.Tail];
        for (var idx = 0; idx < markings.Count; idx++)
        {
            var current = markings[idx].MarkingId;
            if (!_prototype.TryIndex<MarkingPrototype>(current, out var proto) || proto.BodyPart != HumanoidVisualLayers.Tail)
                continue;
            var target = wagging.Wagging
                ? current.EndsWith(wagging.Suffix) ? current : current + wagging.Suffix
                : current.EndsWith(wagging.Suffix) ? current[..^wagging.Suffix.Length] : current;
            if (current != target && _prototype.HasIndex<MarkingPrototype>(target))
                _humanoidAppearance.SetMarkingId(uid, MarkingCategories.Tail, idx, target, humanoid: humanoid);
        }
        return true;
    }
}
