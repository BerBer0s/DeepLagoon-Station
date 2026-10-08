using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared.Clothing;
using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.Loadouts;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Content.Shared.Inventory;

namespace Content.Client.Lobby.UI.Loadouts;

public sealed partial class PersonalLoadoutEditor
{
    public bool TguiMode;
    private readonly TguiSpriteImages _images = new();

    public IEnumerable<TguiData> CreateTguiSlots(EntityUid preview)
    {
        var inventory = _entities.System<InventorySystem>();
        if (!_entities.EntityExists(preview) || !inventory.TryGetSlots(preview, out var slots)) yield break;
        foreach (var slot in slots.Where(s => (s.SlotFlags & (SlotFlags.PREVENTEQUIP | SlotFlags.POCKET | SlotFlags.SUITSTORAGE)) == 0)
                     .OrderBy(s => s.StrippingWindowPos.Y).ThenBy(s => s.StrippingWindowPos.X))
            yield return new TguiData().String("id", slot.Name).String("name", SlotLabel(slot)).Bool("selected", _slotFilter?.Name == slot.Name)
                .Array("images", inventory.TryGetSlotEntity(preview, slot.Name, out var item) ? _images.EntityImages(item.Value) : Enumerable.Empty<TguiData>());
    }

    public bool SelectTguiSlot(EntityUid preview, string name)
    {
        var inventory = _entities.System<InventorySystem>();
        if (!inventory.TryGetSlots(preview, out var slots)) return false;
        var slot = slots.FirstOrDefault(s => s.Name == name && (s.SlotFlags & (SlotFlags.PREVENTEQUIP | SlotFlags.POCKET | SlotFlags.SUITSTORAGE)) == 0);
        if (slot == null) return false;
        _slotFilter = slot;
        _pendingSlotCategory = null;
        UpdateSlotHighlights();
        SlotOpened?.Invoke();
        return true;
    }

    public TguiData CreateTguiState()
    {
        var data = new TguiData();
        if (_profile == null) return data;
        PrepareJobCatalog();
        var role = GetRole();
        var system = _entities.System<PersonalLoadoutSystem>();
        var selected = role.SelectedLoadouts.Values.SelectMany(group => group).ToDictionary(item => item.Prototype.Id);
        var spent = selected.Values.Sum(item => _prototypes.Index(item.Prototype).PersonalCost);
        data.String("job", _job).String("roleName", _jobRole?.EntityName).Bool("customRoleName", _roleName.Visible)
            .String("balance", _jobBalance.Text).String("cost", _jobCost.Text)
            .Number("points", Math.Max(0, system.Points - spent)).Number("maxPoints", system.Points)
            .String("slot", _slotFilter == null ? "" : SlotLabel(_slotFilter))
            .Array("jobs", _equipmentJobs.Select(id => new TguiData().String("id", id).String("name", _prototypes.Index<JobPrototype>(id).LocalizedName)))
            .Array("categories", _prototypes.EnumeratePrototypes<PersonalLoadoutCategoryPrototype>().Select(category =>
                new TguiData().String("id", category.ID).String("name", Loc.TryGetString("loadout-category-" + category.ID, out var name) ? name : category.ID)
                    .Array("children", category.SubCategories.Select(id => new TguiData().String("id", id.Id))).Bool("root", category.Root)))
            .Array("items", _prototypes.EnumeratePrototypes<LoadoutPrototype>().Where(item => item.PersonalItems.Count > 0 && (_slotFilter == null || FitsSlot(item, _slotFilter)))
                .Select(item =>
                {
                    var allowed = system.CanUse(item, _profile, _job, _session, out var reason);
                    selected.TryGetValue(item.ID, out var choice);
                    var donor = DonorCategoryOf(item);
                    var donorText = donor == DonorCategory.Boosty ? Loc.GetString("dl-loadout-donor-boosty", ("tier", item.PersonalDonorTier))
                        : donor == DonorCategory.DiscordBoost ? Loc.GetString("dl-loadout-donor-discord") : "";
                    return new TguiData().String("id", item.ID).String("name", _prototypes.Index(item.PersonalItems[0]).Name)
                        .String("category", item.PersonalCategory).Bool("selected", choice != null).Bool("allowed", allowed)
                        .Bool("canSelect", choice != null || allowed && spent + item.PersonalCost <= system.Points)
                        .String("reason", reason).String("donor", donorText).Number("cost", item.PersonalCost)
                        .Bool("canPaint", item.PersonalCustomColor).Bool("canRename", item.PersonalCustomName).Bool("canDescribe", item.PersonalCustomDescription)
                        .Bool("canHeirloom", item.PersonalHeirloom).Bool("heirloom", choice?.Customization?.Heirloom ?? false)
                        .String("color", choice?.Customization?.Color).String("customName", choice?.Customization?.Name).String("description", choice?.Customization?.Description)
                        .Array("images", _images.Item(item.PersonalItems[0].Id));
                }))
            .Array("jobItems", _jobEntries.Where(entry => _slotFilter == null || FitsJobSlot(entry.Item, _slotFilter)).Select(entry =>
            {
                var item = entry.Item;
                var valid = _jobRole!.IsValid(_profile, _session, item.ID, IoCManager.Instance!, out var reason);
                var chosen = _jobRole.SelectedLoadouts.TryGetValue(entry.Group.ID, out var choices) && choices.Any(choice => choice.Prototype.Id == item.ID);
                var entity = item.PreviewEntity ?? item.DummyEntity ?? _entities.System<LoadoutSystem>().GetFirstOrNull(item);
                return new TguiData().String("id", item.ID).String("group", entry.Group.ID).String("groupName", Loc.GetString(entry.Group.Name))
                    .String("name", JobItemName(item)).String("category", entry.Category).Bool("selected", chosen).Bool("allowed", valid).Bool("canSelect", valid || chosen)
                    .String("reason", reason?.ToString()).Number("cost", item.Price).Number("min", entry.Group.MinLimit).Number("max", entry.Group.MaxLimit)
                    .Array("images", entity is {} id ? _images.Item(id.Id) : Enumerable.Empty<TguiData>());
            }));
        return data;
    }

    public bool HandleTguiAction(string action, TguiActionData args)
    {
        if (_profile == null) return false;
        if (action == "all-slots") { _slotFilter = null; UpdateSlotHighlights(); return true; }
        if (action == "remove-unavailable") { RemoveUnavailable(); return true; }
        if (action == "job")
        {
            if (args.String("value") is not {} job || !_equipmentJobs.Contains(job)) return false;
            JobChanged?.Invoke(job); return true;
        }
        if (action == "role-name")
        {
            if (_jobRole == null || !_roleName.Visible || args.String("value") is not {} name || name.Length > HumanoidCharacterProfile.MaxLoadoutNameLength) return false;
            _jobRole.EntityName = name; RoleNameChanged?.Invoke(_jobRole); return true;
        }
        if (args.String("id") is not {} id) return false;
        if (action == "job-select")
        {
            var entry = _jobEntries.FirstOrDefault(entry => entry.Item.ID == id && entry.Group.ID == args.String("group"));
            if (entry == null || _jobRole == null) return false;
            var role = _jobRole.Clone();
            var chosen = role.SelectedLoadouts.TryGetValue(entry.Group.ID, out var choices) && choices.Any(choice => choice.Prototype.Id == id);
            if (chosen) role.RemoveLoadout(entry.Group.ID, id, _prototypes);
            else
            {
                if (!role.IsValid(_profile, _session, id, IoCManager.Instance!, out _)) return false;
                role.AddLoadout(entry.Group.ID, id, _prototypes);
            }
            SelectionChanged?.Invoke(role); return true;
        }
        if (!_prototypes.TryIndex<LoadoutPrototype>(id, out var prototype) || prototype.PersonalItems.Count == 0) return false;
        var updated = GetRole();
        var selected = updated.SelectedLoadouts.Values.SelectMany(group => group).FirstOrDefault(item => item.Prototype.Id == id);
        var system = _entities.System<PersonalLoadoutSystem>();
        if (action == "select")
        {
            if (selected == null && (!system.CanUse(prototype, _profile, _job, _session, out _) ||
                updated.SelectedLoadouts.Values.SelectMany(group => group).Sum(item => _prototypes.Index(item.Prototype).PersonalCost) + prototype.PersonalCost > system.Points)) return false;
            Select(prototype, selected == null); return true;
        }
        if (selected == null || !system.CanUse(prototype, _profile, _job, _session, out _)) return false;
        var customization = selected.Customization ?? new PersonalLoadoutCustomization();
        if (action == "paint")
        {
            if (!prototype.PersonalCustomColor) return false;
            var color = args.String("value");
            if (!string.IsNullOrEmpty(color) && Color.TryFromHex(color) == null) return false;
            customization = new PersonalLoadoutCustomization { Name = customization.Name, Description = customization.Description, Color = string.IsNullOrEmpty(color) ? null : color, Heirloom = customization.Heirloom };
        }
        else if (action == "rename")
            customization = new PersonalLoadoutCustomization { Name = args.String("name"), Description = args.String("description"), Color = customization.Color, Heirloom = customization.Heirloom };
        else if (action == "heirloom" && prototype.PersonalHeirloom)
            customization = new PersonalLoadoutCustomization { Name = customization.Name, Description = customization.Description, Color = customization.Color, Heirloom = !customization.Heirloom };
        else return false;
        foreach (var group in updated.SelectedLoadouts.Values)
            for (var i = 0; i < group.Count; i++)
                if (group[i].Prototype.Id == id) group[i] = new Loadout { Prototype = id, Customization = system.Sanitize(prototype, customization) };
        SelectionChanged?.Invoke(updated);
        return true;
    }
}
