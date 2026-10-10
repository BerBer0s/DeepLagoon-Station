using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Robust.Shared.Network;

namespace Content.Server.Database;

public sealed partial class ServerDbManager
{
    public Task<LagoonCoinOperation?> ClaimLagoonCoinWebhookAsync(Guid owner, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.ClaimLagoonCoinWebhookAsync(owner, cancel));
    }

    public Task CompleteLagoonCoinWebhookAsync(Guid user, string key, Guid owner, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.CompleteLagoonCoinWebhookAsync(user, key, owner, cancel));
    }

    public Task<MoneyTransfer> TransferMoneyAsync(NetUserId sender, NetUserId recipient, long amount,
        CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.TransferMoneyAsync(sender, recipient, amount, cancel));
    }

    public Task<MoneyPayment> PayMoneyWithBankAsync(NetUserId user, int bankBalance, int amount,
        CancellationToken cancel = default, int? profileSlot = null, bool useSavings = true)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.PayMoneyWithBankAsync(user, bankBalance, amount, cancel, profileSlot, useSavings));
    }

    public Task<long> DepositMoneyWithBankAsync(NetUserId user, int profileSlot, int expectedBankBalance,
        int bankAmount, long moneyAmount, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.DepositMoneyWithBankAsync(user, profileSlot, expectedBankBalance, bankAmount, moneyAmount, cancel));
    }

    public Task<long> GetLagoonCoinsAsync(NetUserId user, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetLagoonCoinsAsync(user, cancel));
    }

    public Task<List<LagoonCoinOperation>> GetLagoonCoinHistoryAsync(NetUserId user, int page, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetLagoonCoinHistoryAsync(user, page, cancel));
    }

    public Task<LagoonCoinResult> AwardLagoonCoinsAsync(NetUserId user, string operationId, long amount, string reason,
        NetUserId? actor = null, long playedTicks = 0, long subscriberTicks = 0, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AwardLagoonCoinsAsync(user, operationId, amount, reason, actor,
            playedTicks, subscriberTicks, cancel));
    }
}
