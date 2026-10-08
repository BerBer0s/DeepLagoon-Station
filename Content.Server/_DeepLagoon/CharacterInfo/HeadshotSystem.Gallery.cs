using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared.DetailExaminable;
using Content.Shared.IdentityManagement;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._DeepLagoon.CharacterInfo;

public sealed partial class HeadshotSystem
{
    public sealed class ImageGallery
    {
        public List<string> Images { get; set; } = new();
        public List<string> Active { get; set; } = new();
    }
    private static ResPath ManifestPath(string id) => new("/headshots/" + Guid.Parse(id).ToString("N") + ".json");
    private int ActiveCapacity(ICommonSession session)
    {
        var supporter = EntityManager.System<SharedDiscordBoostSystem>();
        return HeadshotLimits.ActiveSlots(supporter.GetSupporterTier(session), supporter.HasActiveBoost(session));
    }
    private int Capacity(ICommonSession session)
    {
        var supporter = EntityManager.System<SharedDiscordBoostSystem>();
        return HeadshotLimits.LibrarySlots(supporter.GetSupporterTier(session), supporter.HasActiveBoost(session));
    }
    private ImageGallery LoadGallery(string id)
    {
        if (!Guid.TryParse(id, out _)) return new();
        foreach (var path in new[] { ManifestPath(id), new ResPath(ManifestPath(id) + ".bak") })
        {
            try
            {
                using var file = _resources.UserData.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var gallery = JsonSerializer.Deserialize<ImageGallery>(file);
                if (gallery != null && gallery.Images.Count <= 10 && gallery.Images.All(image => Guid.TryParse(image, out _)) && gallery.Active.All(gallery.Images.Contains)) return gallery;
            }
            catch (Exception) { }
        }
        // Migrate an existing single image without losing it when the primary alias changes.
        try
        {
            var bytes = ImageBytes(id);
            var imageId = Guid.NewGuid().ToString("N");
            using (var file = _resources.UserData.Open(PathFor(imageId), FileMode.Create)) file.Write(bytes);
            var gallery = new ImageGallery { Images = [imageId], Active = [imageId] };
            SaveGallery(id, gallery);
            return gallery;
        }
        catch (Exception) { return new(); }
    }
    private byte[] ImageBytes(string id)
    {
        using var file = _resources.UserData.Open(PathFor(id), FileMode.Open, FileAccess.Read, FileShare.Read);
        using var buffer = new MemoryStream(); file.CopyTo(buffer); return buffer.ToArray();
    }
    private void SaveGallery(string id, ImageGallery gallery)
    {
        var path = ManifestPath(id); var temporary = new ResPath(path + ".tmp"); var backup = new ResPath(path + ".bak");
        using (var file = _resources.UserData.Open(temporary, FileMode.Create)) JsonSerializer.Serialize(file, gallery);
        if (_resources.UserData.Exists(path))
        {
            _resources.UserData.Delete(backup);
            _resources.UserData.Rename(path, backup);
        }
        _resources.UserData.Rename(temporary, path);
    }
    private void CopyPrimary(string id, ImageGallery gallery)
    {
        if (gallery.Active.FirstOrDefault() is not {} primary) { _resources.UserData.Delete(PathFor(id)); return; }
        var bytes = ImageBytes(primary);
        using var file = _resources.UserData.Open(PathFor(id), FileMode.Create); file.Write(bytes);
    }
    public List<string> GetActive(string id, ICommonSession? session = null) => LoadGallery(id).Active.Take(session == null ? 4 : ActiveCapacity(session)).ToList();

    private void Gallery(HeadshotGalleryRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (ev.Target is {} target)
        {
            var entity = GetEntity(target);
            if (ev.Action.Length > 0 || session.AttachedEntity is not {} user || !Exists(entity) || !_examine.CanExamine(user, entity) ||
                !_examine.IsInDetailsRange(user, entity) || !TryComp<DetailExaminableComponent>(entity, out var details) || Identity.Name(entity, EntityManager) != MetaData(entity).EntityName) return;
            RaiseNetworkEvent(new HeadshotGalleryDataEvent(ev.Request, details.HeadshotImages.ToArray(), details.HeadshotImages.ToArray(), 0, 0), session.Channel);
            return;
        }
        var profile = Profile(session, ev.Slot);
        if (profile == null) return;
        var gallery = LoadGallery(profile.HeadshotId);
        var error = "";
        try
        {
            if (ev.Action.Length > 0)
            {
                if (!gallery.Images.Contains(ev.ImageId)) throw new InvalidDataException();
                switch (ev.Action)
                {
                    case "primary":
                        gallery.Active.Remove(ev.ImageId);
                        gallery.Active.Insert(0, ev.ImageId);
                        gallery.Active = gallery.Active.Take(ActiveCapacity(session)).ToList(); break;
                    case "toggle":
                        if (!gallery.Active.Remove(ev.ImageId))
                        {
                            if (gallery.Active.Count >= ActiveCapacity(session)) { error = "Достигнут лимит изображений в профиле."; break; }
                            gallery.Active.Add(ev.ImageId);
                        }
                        if (gallery.Active.Count == 0) gallery.Active.Add(ev.ImageId); break;
                    case "delete":
                        gallery.Images.Remove(ev.ImageId); gallery.Active.Remove(ev.ImageId);
                        if (gallery.Active.Count == 0 && gallery.Images.Count > 0) gallery.Active.Add(gallery.Images[0]); break;
                    default: throw new InvalidDataException();
                }
                gallery.Active = gallery.Active.Take(ActiveCapacity(session)).ToList();
                SaveGallery(profile.HeadshotId, gallery);
                CopyPrimary(profile.HeadshotId, gallery);
                if (ev.Action == "delete") _resources.UserData.Delete(PathFor(ev.ImageId));
            }
        }
        catch (Exception) { error = "Не удалось изменить библиотеку изображений."; }
        RaiseNetworkEvent(new HeadshotGalleryDataEvent(ev.Request, gallery.Images.ToArray(), gallery.Active.Take(ActiveCapacity(session)).ToArray(), Capacity(session), ActiveCapacity(session), error), session.Channel);
    }
}
