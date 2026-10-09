using Content.Shared._DeepLagoon.Currency;
using Robust.Shared.Network;

namespace Content.Client._DeepLagoon.Currency;

public sealed class LagoonCoinSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    public long Balance { get; private set; } = -1;
    public event Action<long>? BalanceUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LagoonCoinBalanceEvent>(ev => SetBalance(ev.Balance));
        _net.Disconnect += OnDisconnected;
    }

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnected;
        base.Shutdown();
    }

    private void OnDisconnected(object? sender, NetDisconnectedArgs args) => SetBalance(-1);
    private void SetBalance(long balance)
    {
        Balance = balance;
        BalanceUpdated?.Invoke(balance);
    }
    public void RequestBalance() => RaiseNetworkEvent(new LagoonCoinBalanceRequest());
}
