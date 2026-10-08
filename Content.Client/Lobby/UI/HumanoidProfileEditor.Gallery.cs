using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared._DeepLagoon.DiscordLink;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly Dictionary<int, HeadshotGalleryDataEvent> _galleries = new();
    private readonly Dictionary<int, Guid> _galleryGenerations = new();
    private readonly Dictionary<int, (int Library, int Active)> _galleryEntitlements = new();
    private readonly Dictionary<string, string> _galleryImages = new();

    private CharacterAuxiliaryWindow? _libraryWindow;
    private int? _librarySlot;

    public TguiData EditorAppearanceState() => _editorAppearance.Data();

    private void CloseHeadshotLibrary()
    {
        var window = _libraryWindow;
        _libraryWindow = null;
        _librarySlot = null;
        window?.Close();
        window?.Dispose();
    }

    private void ToggleHeadshotLibrary()
    {
        if (_libraryWindow != null) { CloseHeadshotLibrary(); return; }
        if (CharacterSlot is not {} slot) return;
        _librarySlot = slot;
        var window = new CharacterAuxiliaryWindow();
        _libraryWindow = window;
        window.OnClose += () =>
        {
            if (_libraryWindow != window) return;
            _libraryWindow = null;
            _librarySlot = null;
            window.Dispose();
            PublishTguiProfile();
        };
        window.Panel.OnAction += (action, payload) =>
        {
            if (_librarySlot != CharacterSlot || Profile == null) return;
            if (action is "headshot-gallery" or "headshot-upload" or "headshot-add" or "headshot-library-close")
                HandleTguiProfileAction(action, payload);
        };
        window.Panel.OnClose += CloseHeadshotLibrary;
        PublishTguiProfile();
        window.OpenCentered();
    }

    private TguiData GalleryState(int slot)
    {
        var supporter = _entManager.System<SharedDiscordBoostSystem>();
        var tier = supporter.GetSupporterTier(_playerManager.LocalSession);
        var booster = supporter.HasActiveBoost(_playerManager.LocalSession);
        var entitlement = (HeadshotLimits.LibrarySlots(tier, booster), HeadshotLimits.ActiveSlots(tier, booster));
        if (!_galleryGenerations.ContainsKey(slot) || _galleryEntitlements.GetValueOrDefault(slot) != entitlement)
        {
            _galleryEntitlements[slot] = entitlement;
            _galleries.Remove(slot);
            RefreshGallery(slot);
        }
        _galleries.TryGetValue(slot, out var gallery);
        return new TguiData().Bool("library", tier >= 2)
            .Number("capacity", gallery?.Capacity ?? HeadshotLimits.LibrarySlots(tier, booster))
            .Number("activeCapacity", gallery?.ActiveCapacity ?? HeadshotLimits.ActiveSlots(tier, booster))
            .String("error", gallery?.Error)
            .Array("images", (gallery?.Images ?? []).Select(id => new TguiData().String("id", id)
                .String("image", _galleryImages.GetValueOrDefault(id)).Bool("active", gallery!.Active.Contains(id))
                .Bool("primary", gallery!.Active.FirstOrDefault() == id)));
    }
    private void RefreshGallery(int slot, string action = "", string imageId = "")
    {
        var generation = Guid.NewGuid(); _galleryGenerations[slot] = generation;
        _entManager.System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Gallery(slot, null, gallery =>
        {
            if (Disposed || _galleryGenerations.GetValueOrDefault(slot) != generation) return;
            _galleries[slot] = gallery;
            if (action.Length > 0)
            {
                _headshotPreviews.Remove(slot); _headshotReads.Remove(slot);
                if (CharacterSlot == slot && Profile != null)
                {
                    _entManager.System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Invalidate(Profile.HeadshotId);
                    if (gallery.Images.Length == 0) _headshotPreviews[slot] = (Profile.HeadshotId, "", "");
                    else EnsureHeadshotPreview(slot, Profile.HeadshotId);
                }
            }
            foreach (var (id, index) in gallery.Images.Select((id, index) => (id, index)))
            {
                Timer.Spawn((index + 1) * 350, () =>
                {
                    if (Disposed || _galleryGenerations.GetValueOrDefault(slot) != generation) return;
                    _entManager.System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Read(slot, null, (bytes, mime, returnedId, error) =>
                    {
                        if (Disposed || _galleryGenerations.GetValueOrDefault(slot) != generation || returnedId != id || error.Length > 0) return;
                        _galleryImages[id] = HeadshotImage(bytes, mime);
                        if (CharacterSlot == slot) PublishTguiProfile();
                    }, id);
                });
            }
            if (CharacterSlot == slot) PublishTguiProfile();
        }, action, imageId);
    }
}
