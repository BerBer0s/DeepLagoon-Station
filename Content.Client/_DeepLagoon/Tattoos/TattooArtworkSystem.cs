using System.Threading.Tasks;
using Content.Shared._DeepLagoon.Tattoos;

namespace Content.Client._DeepLagoon.Tattoos;

public sealed class TattooArtworkSystem : EntitySystem
{
    private readonly Dictionary<Guid, TaskCompletionSource<Tattoo>> _pending = new();
    public override void Initialize() => SubscribeNetworkEvent<TattooArtworkReply>(OnReply);
    private void OnReply(TattooArtworkReply ev)
    {
        if (!_pending.TryGetValue(ev.Request, out var pending)) return;
        if (ev.Artwork is { } artwork && artwork.IsValid()) pending.TrySetResult(artwork);
        else pending.TrySetException(new InvalidOperationException(ev.Error));
    }
    public async Task<Tattoo> Authorize(Tattoo basis, string recipe = "")
    {
        if (_pending.Count >= 2) throw new InvalidOperationException("Дождитесь обработки текущего рисунка.");
        var id = Guid.NewGuid(); var pending = new TaskCompletionSource<Tattoo>(); _pending.Add(id, pending);
        try { RaiseNetworkEvent(new TattooArtworkRequest(id, basis.Clone(), recipe)); return await pending.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { _pending.Remove(id); }
    }
}
