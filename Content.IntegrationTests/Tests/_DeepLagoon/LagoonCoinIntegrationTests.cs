using System.Linq;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Asynchronous;
using ServerWallet = Content.Server._DeepLagoon.Currency.LagoonCoinSystem;
using ClientWallet = Content.Client._DeepLagoon.Currency.LagoonCoinSystem;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class LagoonCoinIntegrationTests
{
    [Test]
    public async Task ReadyAndCompletedAntagonistGoalPublishOnceAndNeverChangeMoney()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        var map = await pair.CreateTestMap();
        EntityUid mob = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.ResolveDependency<IEntityManager>();
            var player = pair.Server.PlayerMan.Sessions.Single();
            var ticker = entities.System<GameTicker>();
            var tasks = pair.Server.ResolveDependency<ITaskManager>();
            var money = pair.Server.ResolveDependency<Content.Server._DeepLagoon.Money.MoneyManager>();
            var setMoney = money.SetMoneyBalanceAsync(player.UserId, 42);
            tasks.BlockWaitOnTask(setMoney);
            setMoney.GetAwaiter().GetResult();

            ticker.ToggleReady(player, true);
            ticker.ToggleReady(player, false);
            ticker.ToggleReady(player, true);
            mob = entities.SpawnEntity("MobHumanDummy", map.GridCoords);
            var profile = HumanoidCharacterProfile.DefaultWithSpecies("Human");
            var spawned = new PlayerSpawnCompleteEvent(mob, player, null, false, false, 1, map.MapUid, profile);
            entities.EventBus.RaiseEvent(EventSource.Local, spawned);
            entities.EventBus.RaiseEvent(EventSource.Local, spawned);
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundStartedEvent(123));
            entities.System<ServerWallet>().Update(0);
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
            Assert.That(pair.Client.ResolveDependency<IEntityManager>().System<ClientWallet>().Balance, Is.EqualTo(5)));

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.ResolveDependency<IEntityManager>();
            var player = pair.Server.PlayerMan.Sessions.Single();
            var mind = entities.System<MindSystem>().GetOrCreateMind(player.UserId);
            entities.System<SharedRoleSystem>().MindAddRole(mind.Owner, "MindRoleTraitor", mind.Comp, silent: true);
            var objective = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            entities.EnsureComponent<ObjectiveComponent>(objective);
            entities.EnsureComponent<FreeObjectiveComponent>(objective);
            mind.Comp.Objectives.Add(objective);
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundEndedEvent(123, TimeSpan.FromMinutes(30)));
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundEndedEvent(123, TimeSpan.FromMinutes(30)));
            entities.System<ServerWallet>().Update(0);
            var database = pair.Server.ResolveDependency<IServerDbManager>();
            Assert.That(database.GetMoneyAsync(player.UserId).GetAwaiter().GetResult(), Is.EqualTo(42));
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
            Assert.That(pair.Client.ResolveDependency<IEntityManager>().System<ClientWallet>().Balance, Is.EqualTo(7)));
        await pair.Server.WaitPost(() => pair.Server.EntMan.DeleteEntity(mob));
        await pair.CleanReturnAsync();
    }
}
