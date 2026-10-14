using System.Collections;
using System.IO;
using System.Net;
using System.Reflection;
using Content.Server._DeepLagoon.DiscordLink;
using Content.Shared.CCVar;
using Microsoft.Data.Sqlite;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class DiscordMergeIsolationTests
{
    [Test]
    public async Task SlowWorkerKeepsApiGateFreeAndSameCodeRecoversAfterHttpDeadline()
    {
        var python = Environment.GetEnvironmentVariable("SS14_MERGE_TEST_PYTHON");
        if (string.IsNullOrEmpty(python) || !File.Exists(python)) Assert.Ignore("Set SS14_MERGE_TEST_PYTHON to a local Python interpreter.");
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        var directory = Path.Combine(Path.GetTempPath(), "ss14-merge-isolation-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var worker = Path.Combine(directory, "worker.py");
        await File.WriteAllTextAsync(worker, """
            import json, pathlib, sys, time
            if '--check' in sys.argv:
                raise SystemExit(0)
            request = json.load(sys.stdin)
            with pathlib.Path(__file__).with_suffix('.runs').open('a') as stream:
                stream.write('run\n')
            time.sleep(25)
            print(json.dumps(dict(ok=True, **request)), flush=True)
            """);
        using var store = new DiscordLinkStore(Path.Combine(directory, "identity.db"));
        const string discord = "1554565156657299597";
        var canonical = store.EnrollLauncher(discord);
        var source = Guid.NewGuid(); var code = store.IssueMerge(source, "MergeTest");
        var system = pair.Server.EntMan.System<DiscordLinkSystem>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var storeField = typeof(DiscordLinkSystem).GetField("_store", flags)!;
        var enabledField = typeof(DiscordLinkSystem).GetField("_enabled", flags)!;
        var oldStore = storeField.GetValue(system); var oldEnabled = enabledField.GetValue(system);
        var method = typeof(DiscordLinkSystem).GetMethod("HandleMergeRequest", flags)!;
        Task request = null;
        async Task Pump(Func<bool> ready, int seconds)
        {
            var until = DateTime.UtcNow.AddSeconds(seconds);
            while (!ready() && DateTime.UtcNow < until) { await pair.RunTicksSync(1); await Task.Delay(20); }
            Assert.That(ready(), Is.True, "Real-time worker deadline exceeded");
        }
        try
        {
            await pair.Server.WaitPost(() =>
            {
                storeField.SetValue(system, store); enabledField.SetValue(system, true);
                pair.Server.CfgMan.SetCVar(CCVars.DiscordAccountMergePython, python!);
                pair.Server.CfgMan.SetCVar(CCVars.DiscordAccountMergeWorker, worker);
                request = (Task)method.Invoke(system, [discord, code])!;
            });
            await Pump(() => File.Exists(Path.ChangeExtension(worker, ".runs")), 15);
            await pair.Server.WaitAssertion(() =>
            {
                var gate = (System.Threading.SemaphoreSlim)typeof(DiscordLinkSystem).GetField("_apiLock", flags)!.GetValue(system)!;
                Assert.That(gate.CurrentCount, Is.EqualTo(1), "A waiting process cannot occupy the shared API gate");
                store.SetSupporter(discord, true, 3, [1, 2, 3, 4]);
                Assert.That(store.BoostyStatus(canonical.Uid).Tier, Is.EqualTo(3));
            });
            await Pump(() => request.IsCompleted, 35); await request;
            var result = request.GetType().GetProperty("Result")!.GetValue(request)!;
            Assert.That(result.GetType().GetProperty("Status")!.GetValue(result), Is.EqualTo(HttpStatusCode.Conflict));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(store.MergePending(source), Is.True, "HTTP timeout must preserve the durable plan");
                var jobs = (IDictionary)typeof(DiscordLinkSystem).GetField("_mergeJobs", flags)!.GetValue(system)!;
                Assert.That(jobs.Count, Is.EqualTo(1));
                request = (Task)method.Invoke(system, [discord, code])!;
            });
            await Pump(() => request.IsCompleted, 20); await request;
            result = request.GetType().GetProperty("Result")!.GetValue(request)!;
            Assert.That(result.GetType().GetProperty("Status")!.GetValue(result), Is.EqualTo(HttpStatusCode.OK));
            await pair.Server.WaitAssertion(() => Assert.That(store.MergePending(source), Is.False));
            Assert.That(await File.ReadAllLinesAsync(Path.ChangeExtension(worker, ".runs")), Has.Length.EqualTo(1), "Retry must await the same worker");
        }
        finally
        {
            // Let this isolated fake worker complete before its temporary store is removed, even after an assertion fails.
            var drained = false;
            var until = DateTime.UtcNow.AddSeconds(35);
            while (!drained && DateTime.UtcNow < until)
            {
                await pair.RunTicksSync(1); await Task.Delay(20);
                await pair.Server.WaitPost(() => drained = ((IDictionary)typeof(DiscordLinkSystem).GetField("_mergeJobs", flags)!.GetValue(system)!).Count == 0);
            }
            await pair.Server.WaitPost(() => { storeField.SetValue(system, oldStore); enabledField.SetValue(system, oldEnabled); });
        }
        await pair.CleanReturnAsync();
        store.Dispose(); SqliteConnection.ClearAllPools(); Directory.Delete(directory, true);
    }
}
