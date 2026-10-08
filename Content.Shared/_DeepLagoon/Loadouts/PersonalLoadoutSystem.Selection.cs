using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Shared._DeepLagoon.Loadouts;

public sealed partial class PersonalLoadoutSystem
{
    /// <summary>Compare actual equipment slots, not catalogue category labels.</summary>
    public bool Conflicts(HumanoidCharacterProfile profile, LoadoutPrototype personal, LoadoutPrototype jobItem)
    {
        if (!personal.PersonalExclusive || personal.PersonalItems.Count == 0)
            return false;
        var species = _prototypes.Index(profile.Species);
        if (!_prototypes.Index(species.Prototype).TryGetComponent<InventoryComponent>(out var inventory) ||
            !_prototypes.TryIndex<InventoryTemplatePrototype>(inventory.TemplateId, out var template))
            return false;
        var equipment = jobItem.Equipment.Keys.AsEnumerable();
        if (_prototypes.TryIndex(jobItem.StartingGear, out var gear))
            equipment = equipment.Concat(gear.Equipment.Keys);
        var slots = template.Slots.Where(slot => equipment.Contains(slot.Name) &&
            (slot.SlotFlags & (SlotFlags.POCKET | SlotFlags.SUITSTORAGE | SlotFlags.PREVENTEQUIP)) == 0);
        return slots.Any(slot => personal.PersonalItems.Any(id =>
            _prototypes.Index(id).TryGetComponent<ClothingComponent>(out var clothing) &&
            (clothing.Slots & slot.SlotFlags) != 0));
    }

    public HumanoidCharacterProfile RemovePersonalConflicts(HumanoidCharacterProfile profile, LoadoutPrototype jobItem)
    {
        if (!profile.Loadouts.TryGetValue(Role, out var stored))
            return profile;
        var personal = stored.Clone();
        var removed = 0;
        foreach (var group in personal.SelectedLoadouts.Values)
            removed += group.RemoveAll(item => _prototypes.TryIndex(item.Prototype, out var proto) && Conflicts(profile, proto, jobItem));
        return removed == 0 ? profile : profile.WithLoadout(personal);
    }

    public RoleLoadout RemoveJobConflicts(HumanoidCharacterProfile profile, RoleLoadout stored, LoadoutPrototype personal)
    {
        var role = stored.Clone();
        foreach (var group in role.SelectedLoadouts.Values)
            group.RemoveAll(item => _prototypes.TryIndex(item.Prototype, out var proto) && Conflicts(profile, personal, proto));
        return role;
    }

    /// <summary>Prevent mandatory job defaults from restoring replaced clothing on validation/spawn.</summary>
    public bool IsOverridden(HumanoidCharacterProfile profile, ProtoId<RoleLoadoutPrototype> role, LoadoutPrototype item, ICommonSession? session)
    {
        if (role.Id == Role || !profile.Loadouts.TryGetValue(Role, out var personal))
            return false;
        var job = _prototypes.EnumeratePrototypes<JobPrototype>().FirstOrDefault(p => LoadoutSystem.GetJobPrototype(p.ID) == role.Id);
        if (job == null)
            return false;
        var valid = personal.Clone();
        valid.EnsureValid(profile, session, IoCManager.Instance!);
        return valid.SelectedLoadouts.Values.SelectMany(group => group).Any(selection =>
            _prototypes.TryIndex(selection.Prototype, out var proto) && CanUse(proto, profile, job.ID, session, out _) && Conflicts(profile, proto, item));
    }
}
