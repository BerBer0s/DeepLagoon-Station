using System.IO;
using Content.Client.Lobby;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared.DetailExaminable;
using Content.Shared.Preferences;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.CharacterInfo;
public sealed class HeadshotSystem : EntitySystem
{
    [Dependency] private IFileDialogManager _dialogs = default!;
    [Dependency] private IClientPreferencesManager _preferences = default!;
    private sealed class Pending(Action<byte[],string,string,string> callback)
    { public Action<byte[],string,string,string> Callback = callback; public MemoryStream Bytes = new(); public int Length = -1; }
    private readonly Dictionary<Guid, Pending> _pending = new();
    public override void Initialize()
    {
        SubscribeNetworkEvent<HeadshotDataEvent>(Receive);
        SubscribeNetworkEvent<CharacterInfoOpenEvent>((ev,_)=>
        {
            var entity=GetEntity(ev.Target);
            if (Exists(entity) && TryComp<DetailExaminableComponent>(entity,out var details))
                CharacterCardWindow.Open(entity, MetaData(entity).EntityName, details.Content, details.OocNotes, details.Erp, details.NonCon, details.Vore, -1);
        });
    }
    private Guid PendingRequest(Action<byte[],string,string,string> callback)
    { var id=Guid.NewGuid(); _pending[id]=new Pending(callback);
      Robust.Shared.Timing.Timer.Spawn(60000,()=> {if (_pending.Remove(id,out var pending)) {pending.Bytes.Dispose();pending.Callback([],"","","Истекло время ожидания изображения.");}});
      return id; }
    public void Read(int slot, NetEntity? target, Action<byte[],string,string,string> callback)
    { RaiseNetworkEvent(new HeadshotRequestEvent(PendingRequest(callback),slot,target)); }
    public async void Upload(int slot, Action<byte[],string,string,string> callback)
    {
        try
        {
            await using var file=await _dialogs.OpenFile(new FileDialogFilters(new FileDialogFilters.Group("png","jpg","jpeg")),FileAccess.Read);
            if (file==null) { callback([],"","",""); return; }
            using var buffer=new MemoryStream(); var chunk=new byte[HeadshotLimits.ChunkBytes]; int size;
            while ((size=await file.ReadAsync(chunk))>0)
            { if (buffer.Length+size>HeadshotLimits.MaxBytes) { callback([],"","","Максимальный размер изображения — 1 МБ."); return; } buffer.Write(chunk,0,size); }
            var bytes=buffer.ToArray(); if (bytes.Length==0) { callback([],"","","Пустой файл."); return; }
            var request=PendingRequest((data,mime,id,error)=>
            {
                if (error.Length==0 && _preferences.Preferences?.Characters.TryGetValue(slot,out var p)==true && p is HumanoidCharacterProfile profile) profile.HeadshotId=id;
                callback(data,mime,id,error);
            });
            RaiseNetworkEvent(new HeadshotBeginEvent(request,slot,bytes.Length));
            for (var offset=0;offset<bytes.Length;offset+=HeadshotLimits.ChunkBytes)
                RaiseNetworkEvent(new HeadshotUploadEvent(request,offset,bytes[offset..Math.Min(bytes.Length,offset+HeadshotLimits.ChunkBytes)]));
        }
        catch (Exception) { callback([],"","","Не удалось открыть изображение."); }
    }
    public async void Download(int slot)
    {
        Read(slot,null,async (bytes,mime,_,error)=>
        {
            if (error.Length>0 || bytes.Length==0) return;
            var output=await _dialogs.SaveFile(new FileDialogFilters(new FileDialogFilters.Group(mime=="image/jpeg"?"jpg":"png")));
            if (output is {} destination) { await using var stream=destination.fileStream; await stream.WriteAsync(bytes); }
        });
    }
    private void Receive(HeadshotDataEvent ev, EntitySessionEventArgs _)
    {
        if (!_pending.TryGetValue(ev.Request,out var pending)) return;
        if (ev.Error.Length>0) { _pending.Remove(ev.Request); pending.Bytes.Dispose(); pending.Callback([],"","",ev.Error); return; }
        if (ev.Length is <0 or >HeadshotLimits.MaxBytes || ev.Offset!=pending.Bytes.Length || ev.Bytes.Length>HeadshotLimits.ChunkBytes || ev.Bytes.Length>ev.Length-ev.Offset || (pending.Length>=0 && pending.Length!=ev.Length))
        { _pending.Remove(ev.Request); pending.Bytes.Dispose(); return; }
        pending.Length=ev.Length; pending.Bytes.Write(ev.Bytes);
        if (pending.Bytes.Length!=ev.Length) return;
        _pending.Remove(ev.Request); var bytes=pending.Bytes.ToArray(); pending.Bytes.Dispose(); pending.Callback(bytes,ev.Mime,ev.Id,"");
    }
}
