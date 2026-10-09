using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public readonly record struct MoneyPayment(long MoneyBalance, int BankCost);
public readonly record struct MoneyTransfer(long SenderBalance, long RecipientBalance);
public readonly record struct LagoonCoinResult(long Balance, long Awarded, bool Applied);

/// <summary>All writers lock the preference row before reading it. Works across server processes.</summary>
public static class AccountCurrencyOperations
{
    private static async Task Lock(ServerDbContext db, Guid user, CancellationToken cancel)
    {
        // An UPDATE takes a row lock in PostgreSQL and a writer lock in SQLite. No cached balance is trusted.
        var count = await db.Preference.Where(p => p.UserId == user)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Money, p => p.Money), cancel);
        if (count != 1)
            throw new InvalidOperationException("Player preferences do not exist.");
    }

    public static async Task<long> ChangeMoney(ServerDbContext db, Guid user, long amount, bool set = false,
        CancellationToken cancel = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        await Lock(db, user, cancel);
        var row = await db.Preference.SingleAsync(p => p.UserId == user, cancel);
        var balance = set ? amount : checked(row.Money + amount);
        if (balance < 0)
            throw new InvalidOperationException("Insufficient Money.");
        row.Money = balance;
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        return balance;
    }

    public static async Task<MoneyTransfer> TransferMoney(ServerDbContext db, Guid sender, Guid recipient,
        long amount, CancellationToken cancel = default)
    {
        if (sender == recipient || amount <= 0)
            throw new ArgumentException("A transfer requires two different accounts and a positive amount.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        // Deterministic locking order prevents opposite-direction transfers from deadlocking.
        await Lock(db, sender.CompareTo(recipient) < 0 ? sender : recipient, cancel);
        await Lock(db, sender.CompareTo(recipient) < 0 ? recipient : sender, cancel);
        var from = await db.Preference.SingleAsync(p => p.UserId == sender, cancel);
        var to = await db.Preference.SingleAsync(p => p.UserId == recipient, cancel);
        if (from.Money < amount)
            throw new InvalidOperationException("Insufficient Money.");
        var recipientBalance = checked(to.Money + amount);
        from.Money -= amount;
        to.Money = recipientBalance;
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        return new MoneyTransfer(from.Money, to.Money);
    }

    /// <summary>Spend savings first; return the remaining cost payable by the character bank.</summary>
    public static async Task<MoneyPayment> PayWithBank(ServerDbContext db, Guid user, int bankBalance,
        int amount, CancellationToken cancel = default)
    {
        if (amount <= 0 || bankBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        await Lock(db, user, cancel);
        var row = await db.Preference.SingleAsync(p => p.UserId == user, cancel);
        var moneyCost = Math.Min(row.Money, amount);
        var bankCost = checked((int)(amount - moneyCost));
        if (bankCost > bankBalance)
            throw new InvalidOperationException("Insufficient Money and bank balance.");
        row.Money -= moneyCost;
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        return new MoneyPayment(row.Money, bankCost);
    }

    public static async Task<LagoonCoinResult> AwardLagoonCoins(ServerDbContext db, Guid user,
        string operationId, long amount, string reason, Guid? actorId = null, long playedTicks = 0,
        long subscriberTicks = 0, CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 200 ||
            string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || amount < 0 ||
            playedTicks < 0 || subscriberTicks < 0 || subscriberTicks > playedTicks)
            throw new ArgumentException("Invalid Lagoon Coin operation.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancel);
        await Lock(db, user, cancel);
        var row = await db.Preference.SingleAsync(p => p.UserId == user, cancel);
        var previous = await db.LagoonCoinOperations.SingleOrDefaultAsync(
            p => p.UserId == user && p.OperationId == operationId, cancel);
        if (previous != null)
        {
            if (previous.PlayedTicks != playedTicks || previous.SubscriberTicks != subscriberTicks ||
                previous.Reason != reason || previous.ActorId != actorId || (playedTicks == 0 && previous.Amount != amount))
                throw new InvalidOperationException("Lagoon Coin operation key was reused with different data.");
            return new LagoonCoinResult(row.LagoonCoins, 0, false);
        }
        var played = checked(row.LagoonCoinPlayedTicks + playedTicks);
        var bonus = checked(row.LagoonCoinBonusTicks + subscriberTicks);
        var hours = played / TimeSpan.TicksPerHour;
        if (hours > 0)
        {
            amount = checked(amount + hours * 2 + bonus / (TimeSpan.TicksPerHour / 2));
            played %= TimeSpan.TicksPerHour;
            bonus %= TimeSpan.TicksPerHour / 2;
        }
        row.LagoonCoins = checked(row.LagoonCoins + amount);
        row.LagoonCoinPlayedTicks = played;
        row.LagoonCoinBonusTicks = bonus;
        db.LagoonCoinOperations.Add(new LagoonCoinOperation
        {
            UserId = user, OperationId = operationId, Amount = amount, Reason = reason,
            ActorId = actorId, PlayedTicks = playedTicks, SubscriberTicks = subscriberTicks, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        return new LagoonCoinResult(row.LagoonCoins, amount, true);
    }
}
