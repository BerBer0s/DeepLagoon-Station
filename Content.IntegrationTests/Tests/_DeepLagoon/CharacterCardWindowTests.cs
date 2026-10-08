using System.Linq;
using Content.Client._DeepLagoon.CharacterInfo;
using Content.Shared.DetailExaminable;
using Content.Shared.Verbs;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Localization;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class CharacterCardWindowTests
{
    [Test]
    public async Task RepeatedOpenReusesWindowAndClosedCardCanReopen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var root = pair.Client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
            var entity = entities.SpawnEntity("MobHumanDummy", MapCoordinates.Nullspace);
            try
            {
                for (var i = 0; i < 20; i++)
                    CharacterCardWindow.Open(entity, "Тестовый персонаж", "Описание", "OOC", 0, 1, 2, 0);
                var first = root.Children.OfType<CharacterCardWindow>().Single();
                first.Close();
                Assert.That(root.Children.OfType<CharacterCardWindow>(), Is.Empty);
                CharacterCardWindow.Open(entity, "Тестовый персонаж", "Описание", "OOC", 0, 1, 2, 0);
                var reopened = root.Children.OfType<CharacterCardWindow>().Single();
                Assert.That(reopened, Is.Not.SameAs(first));
                reopened.Close();
            }
            finally { entities.DeleteEntity(entity); }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PredictedExamineReplaysDoNotOpenCardsAndServerVerbOpensOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, Connected = true });
        var client = pair.Client;
        var server = pair.Server;
        NetEntity target = default;
        await server.WaitPost(() =>
        {
            var entities = server.ResolveDependency<IEntityManager>();
            entities.System<SharedMapSystem>().CreateMap(out var map);
            var entity = entities.SpawnEntity("MobHumanDummy", new MapCoordinates(0, 0, map));
            var details = entities.EnsureComponent<DetailExaminableComponent>(entity);
            details.CharacterCard = true;
            details.Content = "Описание";
            entities.Dirty(entity, details);
            server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), entity);
            target = entities.GetNetEntity(entity);
        });
        await pair.RunTicksSync(30);
        ExamineVerb? verb = null;
        await client.WaitAssertion(() =>
        {
            var entities = client.ResolveDependency<IEntityManager>();
            var entity = entities.GetEntity(target);
            verb = entities.System<Content.Client.Verbs.VerbSystem>().GetLocalVerbs(entity, entity, typeof(ExamineVerb))
                .Single(v => v.Text == client.ResolveDependency<ILocalizationManager>().GetString("detail-examinable-verb-text")) as ExamineVerb;
            Assert.That(verb, Is.Not.Null);
            // Reconciliation repeats the client-side delegate without a new user click.
            for (var i = 0; i < 20; i++) verb!.Act!.Invoke();
        });
        await pair.RunTicksSync(30);
        await client.WaitAssertion(() =>
        {
            var root = client.ResolveDependency<IUserInterfaceManager>().WindowRoot;
            Assert.That(root.Children.OfType<CharacterCardWindow>(), Is.Empty);
            client.ResolveDependency<IEntityManager>().RaisePredictiveEvent(new ExecuteVerbEvent(target, verb!));
        });
        await pair.RunTicksSync(30);
        await client.WaitAssertion(() => client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
            .OfType<CharacterCardWindow>().Single().Close());
        await pair.RunTicksSync(100);
        await client.WaitAssertion(() => Assert.That(client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
            .OfType<CharacterCardWindow>(), Is.Empty));
        // Detach while session data is still present, before pool shutdown clears it.
        await server.WaitPost(() => server.PlayerMan.SetAttachedEntity(server.PlayerMan.Sessions.Single(), null));
        await pair.CleanReturnAsync();
    }
}
