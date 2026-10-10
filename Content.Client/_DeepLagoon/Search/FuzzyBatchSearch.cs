using System.Diagnostics;
using System.Linq;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.Search;

internal interface IFuzzyBatchJob
{
    /// <summary>Does some work until the deadline (a <see cref="Stopwatch"/> timestamp). False when the job is over.</summary>
    bool Update(long now, long deadline);
}

/// <summary>
/// A search over a big list that does not block a frame: it waits for the text to settle, then matches the list in
/// chunks, a few milliseconds per frame, and calls back once with the final result. A new <see cref="Start"/> drops
/// the running search; until the callback the window keeps the old list. Short lists and empty queries are
/// answered at once, inside <see cref="Start"/>. The search is dropped when the owner is disposed or leaves the UI
/// tree, so nothing keeps running after its window is gone.
/// </summary>
public sealed class FuzzyBatchSearch<T> : IFuzzyBatchJob
{
    private readonly Control _owner;
    private FuzzyScan<T>? _scan;
    private Action<List<T>>? _done;
    private bool _byRank;
    private bool _ownerWasInTree;
    private long _startAt;

    public FuzzyBatchSearch(Control owner)
    {
        _owner = owner;
    }

    /// <param name="byRank">True: best matches first. False: the incoming order.</param>
    public void Start(
        IReadOnlyList<T> items,
        string? query,
        Func<T, string?> name,
        Func<T, string?>? extra,
        Action<List<T>> done,
        bool byRank = true)
    {
        Cancel();

        var prepared = FuzzySearch.Prepare(query);
        if (prepared.IsEmpty)
        {
            done(items.ToList());
            return;
        }

        var scan = new FuzzyScan<T>(items, prepared, name, extra, null);
        if (items.Count <= FuzzySearch.BatchListThreshold)
        {
            scan.Step(int.MaxValue);
            done(byRank ? scan.Ranked() : scan.InOrder());
            return;
        }

        _scan = scan;
        _done = done;
        _byRank = byRank;
        _ownerWasInTree = _owner.IsInsideTree;
        _startAt = Stopwatch.GetTimestamp() + FuzzySearch.BatchSettleDelayMs * Stopwatch.Frequency / 1000;
        FuzzyBatchRunner.Add(this);
    }

    /// <summary>Drops the running search without calling back.</summary>
    public void Cancel()
    {
        _scan = null;
        _done = null;
        FuzzyBatchRunner.Remove(this);
    }

    bool IFuzzyBatchJob.Update(long now, long deadline)
    {
        var scan = _scan;
        if (scan == null)
            return false;

        if (_owner.Disposed || _ownerWasInTree && !_owner.IsInsideTree)
        {
            Cancel();
            return false;
        }

        if (now < _startAt)
            return true;

        do
        {
            scan.Step(FuzzySearch.BatchChunkSize);
        } while (!scan.Done && Stopwatch.GetTimestamp() < deadline);

        if (!scan.Done)
            return true;

        var done = _done;
        var result = _byRank ? scan.Ranked() : scan.InOrder();
        Cancel();
        done?.Invoke(result);
        return false;
    }
}

/// <summary>The running batched searches. Driven once per frame by <see cref="FuzzyBatchController"/>.</summary>
internal static class FuzzyBatchRunner
{
    private static readonly List<IFuzzyBatchJob> Jobs = new();
    private static readonly List<IFuzzyBatchJob> Snapshot = new();

    public static void Add(IFuzzyBatchJob job)
    {
        if (!Jobs.Contains(job))
            Jobs.Add(job);
    }

    public static void Remove(IFuzzyBatchJob job)
    {
        Jobs.Remove(job);
    }

    public static void Run()
    {
        if (Jobs.Count == 0)
            return;

        var now = Stopwatch.GetTimestamp();
        var deadline = now + FuzzySearch.BatchBudgetMs * Stopwatch.Frequency / 1000;

        // A callback may start or cancel searches, so walk a copy.
        Snapshot.Clear();
        Snapshot.AddRange(Jobs);
        foreach (var job in Snapshot)
        {
            if (!job.Update(now, deadline))
                Jobs.Remove(job);
        }

        Snapshot.Clear();
    }
}
