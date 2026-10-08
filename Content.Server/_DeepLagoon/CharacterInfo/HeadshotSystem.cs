using System.IO;
using System.Linq;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._DeepLagoon.CharacterInfo;
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
public sealed class HeadshotSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _preferences = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    private sealed record Upload(Guid Request, int Slot, byte[] Buffer, TimeSpan Expires) { public int Offset; }
    private readonly Dictionary<NetUserId, Upload> _uploads = new();
    private readonly Dictionary<NetUserId, TimeSpan> _requests = new();
    private readonly Dictionary<NetUserId, TimeSpan> _reads = new();
    public override void Initialize()
    {
        SubscribeNetworkEvent<HeadshotBeginEvent>(Begin);
        SubscribeNetworkEvent<HeadshotUploadEvent>(Chunk);
        SubscribeNetworkEvent<HeadshotRequestEvent>(Read);
        _resources.UserData.CreateDir(new ResPath("/headshots"));
    }
    private HumanoidCharacterProfile? Profile(ICommonSession session, int slot) =>
        _preferences.TryGetCachedPreferences(session.UserId, out var prefs) && prefs.Characters.TryGetValue(slot, out var p) ? p as HumanoidCharacterProfile : null;
    public static ResPath PathFor(string id) => new("/headshots/" + Guid.Parse(id).ToString("N"));
    public void Delete(string id) { if (Guid.TryParse(id, out _)) _resources.UserData.Delete(PathFor(id)); }
    public static string Validate(byte[] bytes)
    {
        if (bytes.Length is < 1 or > HeadshotLimits.MaxBytes) throw new InvalidDataException();
        var info = Image.Identify(bytes);
        var mime = info.Metadata.DecodedImageFormat?.DefaultMimeType;
        if (mime is not ("image/png" or "image/jpeg") || (long)info.Width * info.Height > HeadshotLimits.MaxPixels || info.Width > 4096 || info.Height > 4096 || info.FrameMetadataCollection.Count > 1) throw new InvalidDataException();
        using var decoded = Image.Load(bytes);
        if (decoded.Frames.Count != 1) throw new InvalidDataException();
        return mime;
    }
    private void Error(ICommonSession session, Guid request, string text) => RaiseNetworkEvent(new HeadshotDataEvent(request, 0, 0, [], "", error:text), session.Channel);
    private void Begin(HeadshotBeginEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (ev.Length is < 1 or > HeadshotLimits.MaxBytes || Profile(session, ev.Slot) == null) { Error(session, ev.Request, "Недопустимый профиль или размер: максимум 1 МБ."); return; }
        if (_requests.TryGetValue(session.UserId, out var until) && until > _timing.CurTime) { Error(session, ev.Request, "Подождите перед следующей загрузкой."); return; }
        foreach (var key in _uploads.Where(p=>p.Value.Expires < _timing.CurTime).Select(p=>p.Key).ToArray()) _uploads.Remove(key);
        if (_uploads.Count >= 16 && !_uploads.ContainsKey(session.UserId)) { Error(session, ev.Request, "Загрузка занята, попробуйте позже."); return; }
        _requests[session.UserId] = _timing.CurTime + TimeSpan.FromSeconds(2);
        _uploads[session.UserId] = new Upload(ev.Request, ev.Slot, new byte[ev.Length], _timing.CurTime + TimeSpan.FromSeconds(60));
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
            var mime=Validate(upload.Buffer);
            var id=Guid.TryParse(profile.HeadshotId, out _) ? profile.HeadshotId : Guid.NewGuid().ToString("N");
            using (var file=_resources.UserData.Open(PathFor(id), FileMode.Create)) file.Write(upload.Buffer);
            profile.HeadshotId=id;
            if (ServerPreferencesManager.ShouldStorePrefs(session.Channel.AuthType))
                await _db.SaveCharacterSlotAsync(session.UserId, profile, upload.Slot);
            Send(session, ev.Request, upload.Buffer, mime, id);
        }
        catch (Exception) { Error(session, ev.Request, "Не удалось сохранить изображение. Используйте PNG/JPEG до 1 МБ, без анимации, до 4096×4096 пикселей."); }
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
        }
        else id=Profile(session, ev.Slot)?.HeadshotId;
        if (!Guid.TryParse(id, out _)) { Send(session, ev.Request, [], "", ""); return; }
        try { using var file=_resources.UserData.Open(PathFor(id),FileMode.Open,FileAccess.Read,FileShare.Read); using var buffer=new MemoryStream(); file.CopyTo(buffer); var bytes=buffer.ToArray(); Send(session, ev.Request, bytes,Validate(bytes),id); }
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
