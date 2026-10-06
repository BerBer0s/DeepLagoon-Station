using System.IO;
using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._DeepLagoon.InteractionPanel;
using Content.Shared._Mono.Company;
using Content.Shared.Guidebook;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private TguiPanel? _profileWeb;
    private TguiEditorHost? _tguiHost;
    private HumanoidCharacterProfile? _publishedTguiProfile;
    private bool _publishedTguiDirty;
    private bool _publishedTguiClothes;
    private float _tguiRefresh;
    private string _tguiMode = "appearance";
    private readonly Dictionary<string, string> _companyWebImages = new();

    private void InitializeTguiEditor()
    {
        _profileWeb = new TguiPanel(suspendWhenHidden: true) { Name = "CharacterTgui" };
        _profileWeb.OnAction += (action, payload) => HandleTguiProfileAction(action, payload);
        // Identity and Save use the original lightweight controls on the left.
        // One browser serves all migrated tabs and never moves between parents.
        for (var tab = 0; tab < 4; tab++)
            foreach (var child in TabContainer.GetChild(tab).Children) child.Visible = false;
        TabContainer.Orphan();
        _tguiHost = new TguiEditorHost(TabContainer, _profileWeb);
        EditorColumn.AddChild(_tguiHost);
        TabContainer.OnTabChanged += SelectTguiTab;
        SelectTguiTab(TabContainer.CurrentTab);
    }

    private void SelectTguiTab(int tab)
    {
        if (_profileWeb == null) return;
        _tguiMode = tab switch { 0 => "appearance", 1 => "jobs", 2 => "traits", 3 => "company", _ => "native" };
        _profileWeb.Visible = _tguiMode != "native";
        _tguiHost?.InvalidateArrange();
        PublishTguiProfile();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _tguiRefresh -= args.DeltaSeconds;
        if (_tguiRefresh > 0 || _profileWeb == null || !_profileWeb.VisibleInTree) return;
        _tguiRefresh = 0.2f;
        if (!ReferenceEquals(Profile, _publishedTguiProfile) || IsDirty != _publishedTguiDirty || ShowClothes.Pressed != _publishedTguiClothes)
            PublishTguiProfile();
    }

    private void PublishTguiProfile()
    {
        if (_profileWeb == null) return;
        _publishedTguiProfile = Profile;
        _publishedTguiDirty = IsDirty;
        _publishedTguiClothes = ShowClothes.Pressed;
        if (_tguiMode != "native")
            _profileWeb.SetState("CharacterEditor", CreateTguiProfileState(_tguiMode).ToString());
    }

    /// <summary>Current draft state; saving still uses LobbyUIController and server preferences.</summary>
    public TguiData CreateTguiProfileState(string mode)
    {
        var data = new TguiData().String("mode", mode).Bool("available", Profile != null)
            .Bool("dirty", IsDirty).Bool("showClothes", ShowClothes.Pressed);
        if (Profile is not { } p) return data;
        var species = _prototypeManager.Index(p.Species);
        data.String("name", p.Name).Number("age", p.Age).String("species", p.Species.Id)
            .Number("sex", (int) p.Sex).Number("gender", (int) p.Gender)
            .Number("spawn", (int) p.SpawnPriority).Number("height", p.Appearance.Height).Number("width", p.Appearance.Width)
            .String("hairStyle", p.Appearance.HairStyleId).String("beardStyle", p.Appearance.FacialHairStyleId)
            .String("hairColor", p.Appearance.HairColor.ToHexNoAlpha()).String("beardColor", p.Appearance.FacialHairColor.ToHexNoAlpha())
            .String("eyeColor", p.Appearance.EyeColor.ToHexNoAlpha()).String("skinColor", p.Appearance.SkinColor.ToHexNoAlpha())
            .Bool("hairLocked", _markingManager.MustMatchSkin(p.Species, HumanoidVisualLayers.Hair, out _, _prototypeManager))
            .Bool("beardLocked", _markingManager.MustMatchSkin(p.Species, HumanoidVisualLayers.FacialHair, out _, _prototypeManager))
            .Number("skinTone", Skin.Value).Bool("humanSkin", species.SkinColoration == HumanoidSkinColor.HumanToned)
            .Number("minAge", species.MinAge).Number("maxAge", species.MaxAge)
            .Number("erp", (int) p.ERPConsent).Number("noncon", (int) p.NonConConsent).Number("vore", (int) p.VoreConsent)
            .Number("unavailable", (int) p.PreferenceUnavailable).String("company", p.Company);
        if (mode == "appearance")
        {
            data.Array("speciesOptions", _species.Select(s => Choice(s.ID, Loc.GetString(s.Name))))
                .Array("sexOptions", species.Sexes.Select(s => Choice(((int) s).ToString(), Loc.GetString($"humanoid-profile-editor-sex-{s.ToString().ToLowerInvariant()}-text"))))
                .Array("genderOptions", Enum.GetValues<Gender>().Select(g => Choice(((int) g).ToString(), Loc.GetString($"humanoid-profile-editor-pronouns-{g.ToString().ToLowerInvariant()}-text"))))
                .Array("spawnOptions", Enum.GetValues<SpawnPriorityPreference>().Select(s => Choice(((int) s).ToString(), Loc.GetString($"humanoid-profile-editor-preference-spawn-priority-{s.ToString().ToLowerInvariant()}"))))
                .Array("hairOptions", HairChoices(false)).Array("beardOptions", HairChoices(true));
        }
        else if (mode == "jobs")
        {
            data.Array("departments", _prototypeManager.EnumeratePrototypes<DepartmentPrototype>().Where(d => !d.EditorHidden)
                .OrderBy(d => Loc.GetString(d.Name)).Select(d => new TguiData().String("id", d.ID).String("name", Loc.GetString(d.Name))
                    .Array("jobs", d.Roles.Select(id => _prototypeManager.Index(id)).Where(j => j.SetPreference)
                        .OrderBy(j => j.LocalizedName).Select(JobData))));
        }
        else if (mode == "traits")
        {
            data.Array("traits", _prototypeManager.EnumeratePrototypes<TraitPrototype>().OrderBy(t => Loc.GetString(t.Name))
                .Where(t => p.TraitPreferences.Contains(t.ID) || CanSelectTguiTrait(t))
                .Select(t => new TguiData().String("id", t.ID).String("name", Loc.GetString(t.Name))
                    .String("description", t.Description is { } description ? Loc.GetString(description) : "")
                    .String("category", t.Category is { } category && _prototypeManager.TryIndex(category, out var cat) ? Loc.GetString(cat.Name) : Loc.GetString("humanoid-profile-editor-traits-default-category"))
                    .Number("cost", t.Cost).Bool("selected", p.TraitPreferences.Contains(t.ID))));
            data.Array("traitPoints", _prototypeManager.EnumeratePrototypes<TraitCategoryPrototype>().Where(c => c.MaxTraitPoints is >= 0)
                .Select(c => new TguiData().String("name", Loc.GetString(c.Name)).Number("max", c.MaxTraitPoints!.Value)
                    .Number("used", p.TraitPreferences.Select(id => _prototypeManager.Index(id)).Where(t => t.Category == c.ID).Sum(t => t.Cost))));
        }
        else if (mode == "company")
        {
            var companies = _prototypeManager.EnumeratePrototypes<CompanyPrototype>().Where(c => !c.Disabled && _companyManager.IsAllowed(c.ID));
            data.Array("companyOptions", companies.OrderBy(c => c.ID == "None" ? "" : c.Name).Select(c => Choice(c.ID, c.Name)));
            if (_prototypeManager.TryIndex<CompanyPrototype>(p.Company, out var company))
                data.String("companyDescriptionHtml", WebChatMessageFormatter.ToHtml(FormattedMessage.FromMarkupPermissive(
                        string.IsNullOrEmpty(company.Description) ? "" : Loc.GetString(company.Description))))
                    .String("companyImage", CompanyDataImage(company));
        }
        return data;
    }

    private static TguiData Choice(string id, string name) => new TguiData().String("id", id).String("name", name);

    private IEnumerable<TguiData> HairChoices(bool beard)
    {
        if (Profile == null) yield break;
        yield return Choice(beard ? HairStyles.DefaultFacialHairStyle : HairStyles.DefaultHairStyle, "—");
        var category = beard ? MarkingCategories.FacialHair : MarkingCategories.Hair;
        foreach (var marking in _markingManager.MarkingsByCategoryAndSpecies(category, Profile.Species).Values
                     .Where(m => _markingManager.CanBeApplied(Profile.Species, Profile.Sex, m, _prototypeManager))
                     .OrderBy(m => Loc.GetString($"marking-{m.ID}")))
            if (marking.ID != (beard ? HairStyles.DefaultFacialHairStyle : HairStyles.DefaultHairStyle))
                yield return Choice(marking.ID, Loc.GetString($"marking-{marking.ID}"));
    }

    private TguiData JobData(JobPrototype job)
    {
        var allowed = _requirements.IsAllowed(job, (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter, out var reason);
        return new TguiData().String("id", job.ID).String("name", job.LocalizedName).String("description", job.LocalizedDescription)
            .Bool("allowed", allowed).String("reason", reason?.ToString() ?? "")
            .Number("priority", (int) (Profile?.JobPriorities.GetValueOrDefault(job.ID, JobPriority.Never) ?? JobPriority.Never));
    }

    private bool CanSelectTguiTrait(TraitPrototype trait)
    {
        if (Profile == null || trait.SpeciesBlacklist.Contains(Profile.Species)) return false;
        return Profile.TraitPreferences.All(id => id == trait.ID ||
            (!trait.MutuallyExclusiveTraits.Contains(id) && !_prototypeManager.Index(id).MutuallyExclusiveTraits.Contains(trait.ID)));
    }

    private string CompanyDataImage(CompanyPrototype company)
    {
        if (string.IsNullOrEmpty(company.Image)) return "";
        if (_companyWebImages.TryGetValue(company.ID, out var cached)) return cached;
        var image = "";
        try
        {
            using var stream = _resManager.ContentFileRead(new ResPath(company.Image));
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            if (buffer.Length <= 512 * 1024 && company.Image.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                image = "data:image/png;base64," + Convert.ToBase64String(buffer.ToArray());
        }
        catch (Exception) { /* Match native UI's optional company image. */ }
        _companyWebImages[company.ID] = image;
        return image;
    }

    /// <summary>All web actions edit the same draft and use the existing save/import/export handlers.</summary>
    public bool HandleTguiProfileAction(string action, string payload)
    {
        if (Profile == null || !TguiActionData.TryParse(payload, out var args)) return false;
        var value = args!.String("value") ?? "";
        var species = _prototypeManager.Index(Profile.Species);
        var valid = true;
        switch (action)
        {
#if DEBUG
            case "dev-select-tab":
                if (!args.TryInt("value", out var tab) || tab < 0 || tab >= TabContainer.ChildCount) return false;
                TabContainer.CurrentTab = tab; break;
#endif
            case "name": if (value.Length > 128) return false; SetName(value); break;
            case "random-name": RandomizeName(); break;
            case "random-all": RandomizeEverything(); break;
            case "save": if (!IsDirty) return false; Save?.Invoke(); break;
            case "reset": ResetToDefault(); break;
            case "import": ImportProfile(); break;
            case "export": ExportProfile(); break;
            case "export-image": ExportImage(); break;
            case "open-images": _resManager.UserData.OpenOsWindow(Content.Client.Sprite.ContentSpriteSystem.Exports); break;
            case "clothes": ShowClothes.SetClickPressed(!ShowClothes.Pressed); ReloadPreview(); break;
            case "species":
                if (!_species.Any(s => s.ID == value)) return false;
                SetSpecies(value); UpdateAgeEdit(); UpdateHairPickers(); UpdateSkinColor(); break;
            case "age":
                if (!args.TryInt("value", out var age) || age < species.MinAge || age > species.MaxAge) return false;
                SetAge(age); UpdateAgeEdit(); break;
            case "sex":
                if (!args.TryInt("value", out var sex) || !species.Sexes.Contains((Sex) sex)) return false;
                SetSex((Sex) sex); UpdateSexControls(); UpdateHairPickers(); break;
            case "gender":
                if (!args.TryInt("value", out var gender) || !Enum.IsDefined((Gender) gender)) return false;
                SetGender((Gender) gender); UpdateGenderControls(); break;
            case "spawn":
                if (!args.TryInt("value", out var spawn) || !Enum.IsDefined((SpawnPriorityPreference) spawn)) return false;
                SetSpawnPriority((SpawnPriorityPreference) spawn); UpdateSpawnPriorityControls(); break;
            case "height": case "width":
                if (!args.TryFloat("value", out var size) || size < 0.8f || size > 1.2f) return false;
                if (action == "height") { SetHeight(size); UpdateHeightControls(); } else { SetWidth(size); UpdateWidthControls(); }
                break;
            case "hair-style": case "beard-style":
                var beard = action == "beard-style";
                var category = beard ? MarkingCategories.FacialHair : MarkingCategories.Hair;
                var empty = beard ? HairStyles.DefaultFacialHairStyle : HairStyles.DefaultHairStyle;
                if (value != empty && (!_markingManager.Markings.TryGetValue(value, out var marking) || marking.MarkingCategory != category ||
                    !_markingManager.CanBeApplied(Profile.Species, Profile.Sex, marking, _prototypeManager))) return false;
                Profile = Profile.WithCharacterAppearance(beard ? Profile.Appearance.WithFacialHairStyleName(value) : Profile.Appearance.WithHairStyleName(value));
                UpdateHairPickers(); UpdateCMarkingsHair(); UpdateCMarkingsFacialHair(); ReloadProfilePreview(); break;
            case "hair-color": case "beard-color": case "eye-color": case "skin-color":
                if (value.Length != 7 || value[0] != '#') return false;
                Color color;
                try { color = Color.FromHex(value); } catch (Exception) { return false; }
                if (action == "hair-color" || action == "beard-color")
                {
                    var layer = action == "hair-color" ? HumanoidVisualLayers.Hair : HumanoidVisualLayers.FacialHair;
                    if (_markingManager.MustMatchSkin(Profile.Species, layer, out _, _prototypeManager)) return false;
                    Profile = Profile.WithCharacterAppearance(action == "hair-color" ? Profile.Appearance.WithHairColor(color) : Profile.Appearance.WithFacialHairColor(color));
                    UpdateHairPickers(); UpdateCMarkingsHair(); UpdateCMarkingsFacialHair(); ReloadProfilePreview();
                }
                else if (action == "eye-color")
                {
                    Profile = Profile.WithCharacterAppearance(Profile.Appearance.WithEyeColor(color)); UpdateEyePickers(); ReloadProfilePreview();
                }
                else
                {
                    if (species.SkinColoration == HumanoidSkinColor.HumanToned) return false;
                    _rgbSkinColorSelector.Color = color; OnSkinColorOnValueChanged();
                }
                break;
            case "skin-tone":
                if (species.SkinColoration != HumanoidSkinColor.HumanToned || !args.TryFloat("value", out var tone) || tone < 0 || tone > 100) return false;
                Skin.Value = tone; OnSkinColorOnValueChanged(); break;
            case "erp": case "noncon": case "vore":
                if (!args.TryInt("value", out var consent) || !Enum.IsDefined((InteractionPanelConsent) consent)) return false;
                var consentCategory = action == "erp" ? InteractionPanelCategory.Erotic : action == "noncon" ? InteractionPanelCategory.NonCon : InteractionPanelCategory.Vore;
                Profile = Profile.WithInteractionPanelConsent(consentCategory, (InteractionPanelConsent) consent); UpdateInteractionPanelPreferences(); SetDirty(); break;
            case "job":
                if (args.String("id") is not { } jobId || !_prototypeManager.TryIndex<JobPrototype>(jobId, out var job) || !job.SetPreference ||
                    !_requirements.IsAllowed(job, (HumanoidCharacterProfile?) _preferencesManager.Preferences?.SelectedCharacter, out _) ||
                    !args.TryInt("value", out var priority) || !Enum.IsDefined((JobPriority) priority)) return false;
                if ((JobPriority) priority == JobPriority.High)
                    foreach (var (id, previous) in Profile.JobPriorities.ToArray())
                        if (id != jobId && previous == JobPriority.High) Profile = Profile.WithJobPriority(id, JobPriority.Medium);
                Profile = Profile.WithJobPriority(jobId, (JobPriority) priority); UpdateJobPriorities(); ReloadPreview(); break;
            case "unavailable":
                if (!args.TryInt("value", out var unavailable) || !Enum.IsDefined((PreferenceUnavailableMode) unavailable)) return false;
                Profile = Profile.WithPreferenceUnavailable((PreferenceUnavailableMode) unavailable); PreferenceUnavailableButton.SelectId(unavailable); SetDirty(); break;
            case "trait":
                if (args.String("id") is not { } traitId || !_prototypeManager.TryIndex<TraitPrototype>(traitId, out var trait)) return false;
                if (Profile.TraitPreferences.Contains(trait.ID)) Profile = Profile.WithoutTraitPreference(trait.ID, _prototypeManager);
                else { if (!CanSelectTguiTrait(trait)) return false; Profile = Profile.WithTraitPreference(trait.ID, _prototypeManager); }
                RefreshTraits(); SetDirty(); break;
            case "clear-traits": Profile = Profile.WithoutAllTraitPreferences(); RefreshTraits(); SetDirty(); break;
            case "company":
                if (!_prototypeManager.TryIndex<CompanyPrototype>(value, out var company) || company.Disabled || !_companyManager.IsAllowed(company.ID)) return false;
                Profile = Profile.WithCompany(value); UpdateCompanyControls(); SetDirty(); break;
            case "species-guide":
                OnSpeciesInfoButtonPressed(null!); break;
            case "job-guide":
                if (args.String("id") is not { } guideJob || !_prototypeManager.TryIndex<JobPrototype>(guideJob, out var guideProto)) return false;
                if (guideProto.Guides is { } guides) OnOpenGuidebook?.Invoke(guides); break;
            default: valid = false; break;
        }
        if (valid) PublishTguiProfile();
        return valid;
    }
}
