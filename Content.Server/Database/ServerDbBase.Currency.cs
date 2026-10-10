using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task RetryLagoonCoinWebhookAsync(Guid user, string key, Guid owner, DateTime retryAt, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        await AccountCurrencyOperations.RetryLagoonCoinWebhook(db.DbContext, user, key, owner, retryAt, cancel);
    }

    public async Task<LagoonCoinOperation?> ClaimLagoonCoinWebhookAsync(Guid owner, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.ClaimLagoonCoinWebhook(db.DbContext, owner, cancel);
    }

    public async Task CompleteLagoonCoinWebhookAsync(Guid user, string key, Guid owner, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        await AccountCurrencyOperations.CompleteLagoonCoinWebhook(db.DbContext, user, key, owner, cancel);
    }

    public async Task<long> GetMoneyAsync(NetUserId userId, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.Preference.Where(p => p.UserId == userId.UserId)
            .Select(p => (long?)p.Money).SingleOrDefaultAsync(cancel) ?? 0;
    }

    public async Task<long> SetMoneyAsync(NetUserId userId, long balance, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.ChangeMoney(db.DbContext, userId.UserId, balance, set: true, cancel: cancel);
    }

    public async Task<long> AddMoneyAsync(NetUserId userId, long amount, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.ChangeMoney(db.DbContext, userId.UserId, amount, cancel: cancel);
    }

    public async Task<MoneyTransfer> TransferMoneyAsync(NetUserId sender, NetUserId recipient, long amount,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.TransferMoney(db.DbContext, sender.UserId, recipient.UserId, amount, cancel);
    }

    public async Task<MoneyPayment> PayMoneyWithBankAsync(NetUserId user, int bankBalance, int amount,
        CancellationToken cancel = default, int? profileSlot = null, bool useSavings = true)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.PayWithBank(db.DbContext, user.UserId, bankBalance, amount, cancel, profileSlot, useSavings);
    }

    public async Task<long> DepositMoneyWithBankAsync(NetUserId user, int profileSlot, int expectedBankBalance,
        int bankAmount, long moneyAmount, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.DepositWithBank(db.DbContext, user.UserId, profileSlot,
            expectedBankBalance, bankAmount, moneyAmount, cancel);
    }

    public async Task<long> GetLagoonCoinsAsync(NetUserId user, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.Preference.Where(p => p.UserId == user.UserId)
            .Select(p => (long?)p.LagoonCoins).SingleOrDefaultAsync(cancel) ?? 0;
    }

    public async Task<List<LagoonCoinOperation>> GetLagoonCoinHistoryAsync(NetUserId user, int page, CancellationToken cancel = default)
    {
        if (page is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(page));
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.GetLagoonCoinHistory(db.DbContext, user.UserId, page, cancel);
    }

    public async Task<LagoonCoinResult> AwardLagoonCoinsAsync(NetUserId user, string operationId, long amount,
        string reason, NetUserId? actor = null, long playedTicks = 0, long subscriberTicks = 0,
        CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);
        return await AccountCurrencyOperations.AwardLagoonCoins(db.DbContext, user.UserId, operationId, amount,
            reason, actor?.UserId, playedTicks, subscriberTicks, cancel);
    }
}
