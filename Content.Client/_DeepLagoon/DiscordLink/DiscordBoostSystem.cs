using Content.Shared._DeepLagoon.DiscordLink;
using Robust.Client;
using Robust.Shared.Player;

namespace Content.Client._DeepLagoon.DiscordLink;

public sealed class DiscordBoostSystem : SharedDiscordBoostSystem
{
    [Dependency] private readonly Robust.Client.Player.IPlayerManager _players = default!;
    [Dependency] private readonly IBaseClient _client = default!;
    private DateTime _expiresAt;

    public override void Initialize()
    {
        base.Initialize();
        _client.RunLevelChanged += OnRunLevelChanged;
        SubscribeNetworkEvent<DiscordBoostStatusEvent>(args =>
            _expiresAt = DateTime.UtcNow.AddSeconds(Math.Max(0, args.RemainingSeconds)));
    }

    public override bool HasActiveBoost(ICommonSession? session)
        => session != null && session == _players.LocalSession && DateTime.UtcNow < _expiresAt;

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        if (args.NewLevel == ClientRunLevel.Initialize)
            _expiresAt = default;
    }

    public override void Shutdown()
    {
        _expiresAt = default;
        _client.RunLevelChanged -= OnRunLevelChanged;
        base.Shutdown();
    }
}
