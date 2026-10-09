using Content.Shared._DeepLagoon.Money;
using Robust.Shared.Network;

namespace Content.Client._DeepLagoon.Money;

/// <summary>
/// Client-side system for handling Money balance requests and responses.
/// </summary>
public sealed partial class MoneyManager
{
    [Dependency] private INetManager _net = default!;

    /// <summary>
    /// The last known Money balance. -1 indicates balance hasn't been fetched yet.
    /// </summary>
    private long _lastKnownBalance = -1;

    /// <summary>
    /// Event raised when Money balance is updated.
    /// </summary>
    public event Action<long>? BalanceUpdated;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgMoney>(OnBalanceGet);
        _net.RegisterNetMessage<MsgMoneyRequest>();
        _net.Disconnect += (_, _) =>
        {
            _lastKnownBalance = -1;
            BalanceUpdated?.Invoke(_lastKnownBalance);
        };
    }

    /// <summary>
    /// Handles Money balance response from server.
    /// </summary>
    private void OnBalanceGet(MsgMoney msg)
    {
        _lastKnownBalance = msg.Coins;
        BalanceUpdated?.Invoke(_lastKnownBalance);
    }

    /// <summary>
    /// Requests the current Money balance from the server.
    /// </summary>
    public void RequestBalance()
    {
        var message = new MsgMoneyRequest();
        _net.ClientSendMessage(message);
    }

    /// <summary>
    /// Gets the last known Money balance.
    /// Returns -1 if balance hasn't been fetched yet.
    /// </summary>
    public long GetLastKnownBalance()
    {
        return _lastKnownBalance;
    }
}
