using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Content.Server._DeepLagoon.DiscordLink;
using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared._DeepLagoon.Loadouts;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using NUnit.Framework;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class DiscordBoostLoadoutTests
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: loadout
          id: TestDiscordBoosterLoadout
          personalRequirements:
          - kind: DiscordBoostRequirement
        """;

    [Test]
    public async Task ServerRejectsSavedBoosterEquipmentAfterRevocation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        var server = pair.Server;
        var systems = server.ResolveDependency<IEntitySystemManager>();
        var linking = systems.GetEntitySystem<DiscordLinkSystem>();
        var loadouts = systems.GetEntitySystem<PersonalLoadoutSystem>();
        var config = server.ResolveDependency<IConfigurationManager>();
        var directory = Path.Combine(Path.GetTempPath(), "ss14-boost-test-" + Guid.NewGuid());
        using var store = new DiscordLinkStore(Path.Combine(directory, "links.db"));
        var storeField = typeof(DiscordLinkSystem).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var enabledField = typeof(DiscordLinkSystem).GetField("_enabled", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await server.WaitPost(() =>
        {
            config.SetCVar(CCVars.AutoVoteEnabled, false);
            config.SetCVar(CCVars.MapAutoVoteEnabled, false);
            config.SetCVar(CCVars.PresetAutoVoteEnabled, false);
            config.SetCVar(CCVars.VoteEnabled, false);
            var votes = server.ResolveDependency<Content.Server.Voting.Managers.IVoteManager>();
            foreach (var vote in votes.ActiveVotes.ToArray())
                vote.Cancel();
            votes.Update();
            config.SetCVar(CCVars.PersonalLoadoutsEnabled, true);
            storeField.SetValue(linking, store);
            enabledField.SetValue(linking, true);
        });
        var session = await server.AddDummySession();
        await PoolManager.WaitUntil(server, () => session.Status == SessionStatus.InGame, 600);
        try
        {
            await server.WaitAssertion(() =>
            {
                // Exercise the authenticated production path with the test transport.
                var auth = session.GetType().GetProperty("AuthType")!;
                auth.SetValue(session, LoginType.LoggedIn);
                const string discord = "1554565156657299597";
                store.Consume(discord, store.Issue(session.UserId.UserId, session.Name));
                var prototype = server.ResolveDependency<IPrototypeManager>().Index<LoadoutPrototype>("TestDiscordBoosterLoadout");
                var profile = HumanoidCharacterProfile.DefaultWithSpecies();
                Assert.That(systems.GetEntitySystem<SharedDiscordBoostSystem>(), Is.SameAs(linking));
                Assert.That(loadouts.CanUse(prototype, profile, "Passenger", session, out _), Is.False);
                store.SetBoost(discord, true);
                Assert.That(loadouts.CanUse(prototype, profile, "Passenger", session, out _), Is.True);
                Assert.That(loadouts.CanUse(prototype, profile, "Passenger", null, out _), Is.False);
                auth.SetValue(session, LoginType.GuestAssigned);
                Assert.That(loadouts.CanUse(prototype, profile, "Passenger", session, out _), Is.False);
                auth.SetValue(session, LoginType.LoggedIn);
                store.SetBoost(discord, false);
                Assert.That(loadouts.CanUse(prototype, profile, "Passenger", session, out var reason), Is.False);
                Assert.That(reason, Does.Contain("Discord"));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                enabledField.SetValue(linking, false);
                storeField.SetValue(linking, null);
            });
            await server.RemoveDummySession(session);
        }
        await pair.CleanReturnAsync();
    }
}
