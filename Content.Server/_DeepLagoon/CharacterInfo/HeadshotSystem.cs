using System.IO;
using System.Linq;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared.DetailExaminable;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Preferences;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;

namespace Content.Server._DeepLagoon.CharacterInfo;
public sealed partial class HeadshotSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    private sealed record Upload(Guid Request, int Slot, byte[] Buffer, TimeSpan Expires, bool Append, string ImageId) { public int Offset; }
    private readonly Dictionary<NetUserId, Upload> _uploads = new();
    private readonly Dictionary<NetUserId, TimeSpan> _requests = new();
    private readonly Dictionary<NetUserId, TimeSpan> _reads = new();
    public override void Initialize()
    {
        SubscribeNetworkEvent<HeadshotBeginEvent>(Begin);
        SubscribeNetworkEvent<HeadshotUploadEvent>(Chunk);
        SubscribeNetworkEvent<HeadshotRequestEvent>(Read);
        SubscribeNetworkEvent<HeadshotGalleryRequestEvent>(Gallery);
        _resources.UserData.CreateDir(new ResPath("/headshots"));
    }
    private HumanoidCharacterProfile? Profile(ICommonSession session, int slot) =>
        _preferences.TryGetCachedPreferences(session.UserId, out var prefs) && prefs.Characters.TryGetValue(slot, out var p) ? p as HumanoidCharacterProfile : null;
    public static ResPath PathFor(string id) => new("/headshots/" + Guid.Parse(id).ToString("N"));
    public void Delete(string id)
    {
        if (!Guid.TryParse(id, out _)) return;
        var gallery = LoadGallery(id);
        foreach (var image in gallery.Images) _resources.UserData.Delete(PathFor(image));
        _resources.UserData.Delete(PathFor(id));
        _resources.UserData.Delete(ManifestPath(id));
        _resources.UserData.Delete(new ResPath(ManifestPath(id) + ".bak"));
    }
    public static string Validate(byte[] bytes, bool extended = false)
    {
        if (bytes.Length < 1 || bytes.Length > (extended ? HeadshotLimits.SponsorMaxBytes : HeadshotLimits.MaxBytes)) throw new InvalidDataException();
        var info = Image.Identify(bytes);
        var mime = info.Metadata.DecodedImageFormat?.DefaultMimeType;
        var animated = extended && mime == "image/gif";
        var frames = info.FrameMetadataCollection.Count;
        if (!(mime is "image/png" or "image/jpeg" || animated) || (long)info.Width * info.Height > HeadshotLimits.MaxPixels || info.Width > 4096 || info.Height > 4096 ||
            (!animated && frames > 1) || frames > HeadshotLimits.MaxFrames || animated && (long)info.Width * info.Height * Math.Max(1, frames) > HeadshotLimits.MaxAnimationPixels) throw new InvalidDataException();
        using var decoded = Image.Load(bytes);
        if (!animated && decoded.Frames.Count != 1) throw new InvalidDataException();
        return mime!;
    }
    private void Error(ICommonSession session, Guid request, string text) => RaiseNetworkEvent(new HeadshotDataEvent(request, 0, 0, [], "", error:text), session.Channel);
    private bool Extended(ICommonSession session) => EntityManager.System<SharedDiscordBoostSystem>().GetSupporterTier(session) >= 2;
    private void Begin(HeadshotBeginEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var maxBytes = Extended(session) ? HeadshotLimits.SponsorMaxBytes : HeadshotLimits.MaxBytes;
        if (ev.Length < 1 || ev.Length > maxBytes || Profile(session, ev.Slot) == null) { Error(session, ev.Request, $"Недопустимый профиль или размер: максимум {maxBytes / (1024 * 1024)} МБ."); return; }
        if (_requests.TryGetValue(session.UserId, out var until) && until > _timing.CurTime) { Error(session, ev.Request, "Подождите перед следующей загрузкой."); return; }
        foreach (var key in _uploads.Where(p=>p.Value.Expires < _timing.CurTime).Select(p=>p.Key).ToArray()) _uploads.Remove(key);
        if (_uploads.Count >= 16 && !_uploads.ContainsKey(session.UserId)) { Error(session, ev.Request, "Загрузка занята, попробуйте позже."); return; }
        _requests[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(2);
        var gallery = LoadGallery(Profile(session, ev.Slot)!.HeadshotId);
        if (ev.Append && gallery.Images.Count >= Capacity(session) || ev.ImageId.Length > 0 && !gallery.Images.Contains(ev.ImageId))
        { Error(session, ev.Request, "Нет свободного места в библиотеке или изображение не принадлежит этому персонажу."); return; }
        _uploads[session.UserId] = new Upload(ev.Request, ev.Slot, new byte[ev.Length], _timing.CurTime + TimeSpan.FromSeconds(60), ev.Append, ev.ImageId);
    }
    private async void Chunk(HeadshotUploadEvent ev, EntitySessionEventArgs args)
    {
        var session=args.SenderSession;
        if (!_uploads.TryGetValue(session.UserId, out var upload) || upload.Request != ev.Request) return;
        if (upload.Expires < _timing.CurTime || ev.Offset != upload.Offset || ev.Bytes.Length is < 1 or > HeadshotLimits.ChunkBytes || ev.Bytes.Length > upload.Buffer.Length-upload.Offset)
        { _uploads.Remove(session.UserId); Error(session, ev.Request, "Передача изображения прервана."); return; }
        ev.Bytes.CopyTo(upload.Buffer, upload.Offset); upload.Offset += ev.Bytes.Length;
        if (upload.Offset != upload.Buffer.Length) return;
        _uploads.Remove(session.UserId);
        var profile=Profile(session, upload.Slot); if (profile == null) return;
        try
        {
            var mime=Validate(upload.Buffer, Extended(session));
            var id=Guid.TryParse(profile.HeadshotId, out _) ? profile.HeadshotId : Guid.NewGuid().ToString("N");
            var gallery = LoadGallery(profile.HeadshotId);
            if (upload.Append && gallery.Images.Count >= Capacity(session)) throw new InvalidDataException();
            var imageId = upload.ImageId.Length > 0 ? upload.ImageId : upload.Append || gallery.Images.Count == 0 ? Guid.NewGuid().ToString("N") : gallery.Active.FirstOrDefault() ?? gallery.Images[0];
            if (upload.ImageId.Length > 0 && !gallery.Images.Contains(imageId)) throw new InvalidDataException();
            using (var file=_resources.UserData.Open(PathFor(imageId), FileMode.Create)) file.Write(upload.Buffer);
            if (!gallery.Images.Contains(imageId)) gallery.Images.Add(imageId);
            if (!gallery.Active.Contains(imageId) && gallery.Active.Count < ActiveCapacity(session)) gallery.Active.Add(imageId);
            gallery.Active = gallery.Active.Take(ActiveCapacity(session)).ToList();
            SaveGallery(id, gallery);
            CopyPrimary(id, gallery);
            profile.HeadshotId=id;
            if (ServerPreferencesManager.ShouldStorePrefs(session.Channel.AuthType))
                await _db.SaveCharacterSlotAsync(session.UserId, profile, upload.Slot);
            var primaryBytes = ImageBytes(id);
            Send(session, ev.Request, primaryBytes, Validate(primaryBytes, extended: true), id);
        }
        catch (Exception) { Error(session, ev.Request, Extended(session)
            ? "Не удалось сохранить изображение. Используйте PNG/JPEG/GIF до 5 МБ и 4096×4096 пикселей. GIF: до 300 кадров и 32 млн пикселей суммарно."
            : "Не удалось сохранить изображение. Используйте PNG/JPEG до 1 МБ, без анимации, до 4096×4096 пикселей."); }
    }
    private void Read(HeadshotRequestEvent ev, EntitySessionEventArgs args)
    {
        var session=args.SenderSession;
        if (_reads.TryGetValue(session.UserId, out var until) && until > _timing.CurTime) { Error(session, ev.Request, "Подождите перед повторным запросом изображения."); return; }
        _reads[session.UserId] = _timing.CurTime + TimeSpan.FromMilliseconds(250);
        string? id;
        if (ev.Target is {} target)
        {
            var entity=GetEntity(target);
            if (session.AttachedEntity is not {} user || !Exists(entity) || !_examine.CanExamine(user, entity) || !_examine.IsInDetailsRange(user, entity) || !TryComp<DetailExaminableComponent>(entity, out var details) || Identity.Name(entity, EntityManager) != MetaData(entity).EntityName) return;
            id=details.HeadshotId;
            if (ev.ImageId.Length > 0)
            {
                if (!details.HeadshotImages.Contains(ev.ImageId)) return;
                id = ev.ImageId;
            }
        }
        else
        {
            id=Profile(session, ev.Slot)?.HeadshotId;
            if (ev.ImageId.Length > 0)
            {
                if (id == null || !LoadGallery(id).Images.Contains(ev.ImageId)) { Error(session, ev.Request, "Изображение не принадлежит этому персонажу."); return; }
                id = ev.ImageId;
            }
        }
        if (!Guid.TryParse(id, out _)) { Send(session, ev.Request, [], "", ""); return; }
        try
        {
            using var file = _resources.UserData.Open(PathFor(id), FileMode.Open, FileAccess.Read, FileShare.Read);
            using var buffer = new MemoryStream(); file.CopyTo(buffer); var bytes = buffer.ToArray();
            if (ev.CachedHash.Length == 64 && ev.CachedHash == HeadshotLimits.ContentHash(bytes))
                RaiseNetworkEvent(new HeadshotDataEvent(ev.Request, 0, 0, [], "", id, notModified: true), session.Channel);
            else Send(session, ev.Request, bytes, Validate(bytes, extended: true), id);
        }
        catch (Exception) { Error(session, ev.Request, "Изображение недоступно."); }
    }
    public override void Update(float frameTime)
    {
        foreach (var key in _uploads.Where(p=>p.Value.Expires < _timing.CurTime).Select(p=>p.Key).ToArray()) _uploads.Remove(key);
        foreach (var key in _requests.Where(p=>p.Value < _timing.CurTime).Select(p=>p.Key).ToArray()) _requests.Remove(key);
        foreach (var key in _reads.Where(p=>p.Value < _timing.CurTime).Select(p=>p.Key).ToArray()) _reads.Remove(key);
    }
    private void Send(ICommonSession session, Guid request, byte[] bytes, string mime, string id)
    {
        if (bytes.Length==0) { RaiseNetworkEvent(new HeadshotDataEvent(request,0,0,[],mime,id),session.Channel); return; }
        for (var offset=0;offset<bytes.Length;offset+=HeadshotLimits.ChunkBytes)
            RaiseNetworkEvent(new HeadshotDataEvent(request,offset,bytes.Length,bytes[offset..Math.Min(bytes.Length,offset+HeadshotLimits.ChunkBytes)],mime,id),session.Channel);
    }
}
