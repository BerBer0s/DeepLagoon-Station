using System.Text.Json;
using Content.Client.Lobby.UI.Loadouts;
using Content.Shared.Preferences;
using System.Linq;
using System.Reflection;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared._DeepLagoon.CharacterInfo;
using Content.Shared._DeepLagoon.DiscordLink;
using Content.Shared._DeepLagoon.Loadouts;
using Robust.Shared.GameObjects;
using ServerHeadshots = Content.Server._DeepLagoon.CharacterInfo.HeadshotSystem;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class HostSupporterAccessTests
{
    [Test]
    public async Task HostUnlocksFeaturesOnBothSidesAndRevocationRemovesAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        var server = pair.Server;
        var admins = server.ResolveDependency<IAdminManager>();
        var session = server.ResolveDependency<Robust.Server.Player.IPlayerManager>().Sessions.Single();
        await server.WaitPost(() => admins.PromoteHost(session));
        await PoolManager.WaitUntil(server, () => admins.GetAdminData(session, true) != null, 600);
        PersonalLoadoutEditor editor = null!;
        await pair.Client.WaitPost(() =>
        {
            editor = new PersonalLoadoutEditor { TguiMode = true };
            editor.Refresh(HumanoidCharacterProfile.DefaultWithSpecies("Human"), "Contractor", pair.Client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalSession);
        });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        async Task SetFlags(AdminFlags permission, bool active)
        {
            await server.WaitPost(() =>
            {
                var data = admins.GetAdminData(session, true)!;
                data.Flags = permission;
                data.Active = active;
                admins.GetType().GetMethod("UpdateAdminStatus", flags)!.Invoke(admins, [session]);
            });
            await pair.RunTicksSync(10);
        }
        async Task Check(bool host)
        {
            await server.WaitAssertion(() =>
            {
                var entities = server.ResolveDependency<IEntityManager>();
                var supporters = entities.System<SharedDiscordBoostSystem>();
                Assert.That(supporters.GetBoostyTier(session), Is.Zero, "Host access must not fabricate a subscription");
                Assert.That(supporters.HasActiveBoost(session), Is.False);
                Assert.That(supporters.GetSupporterTier(session), Is.EqualTo(host ? 3 : 0));
                Assert.That(supporters.HasDiscordRewardAccess(session), Is.EqualTo(host));
                foreach (var tier in new[] { 1, 2, 3 })
                    Assert.That(supporters.CanUseDonorItem(DonorCategory.Boosty, tier, session), Is.EqualTo(host));
                var loadouts = entities.System<PersonalLoadoutSystem>();
                Assert.That(loadouts.GetPoints(session), Is.EqualTo(loadouts.Points + (host ? 12 : 0)));
                var headshots = entities.System<ServerHeadshots>();
                Assert.That(headshots.GetType().GetMethod("Extended", flags)!.Invoke(headshots, [session]), Is.EqualTo(host));
                Assert.That(headshots.GetType().GetMethod("Capacity", flags)!.Invoke(headshots, [session]), Is.EqualTo(host ? 10 : 1));
                Assert.That(headshots.GetType().GetMethod("ActiveCapacity", flags)!.Invoke(headshots, [session]), Is.EqualTo(host ? 4 : 1));
            });
            await pair.Client.WaitAssertion(() =>
            {
                var local = pair.Client.ResolveDependency<Robust.Client.Player.IPlayerManager>().LocalSession;
                var entities = pair.Client.ResolveDependency<IEntityManager>();
                var supporters = entities.System<SharedDiscordBoostSystem>();
                Assert.That(supporters.GetSupporterTier(local), Is.EqualTo(host ? 3 : 0));
                Assert.That(supporters.CanUseDonorItem(DonorCategory.DiscordBoost, 1, local), Is.EqualTo(host));
                foreach (var tier in new[] { 1, 2, 3 })
                    Assert.That(supporters.CanUseDonorItem(DonorCategory.Boosty, tier, local), Is.EqualTo(host));
                Assert.That(entities.System<Content.Client._DeepLagoon.CharacterInfo.HeadshotSystem>().Extended, Is.EqualTo(host));
                using var state = JsonDocument.Parse(editor.CreateTguiState().ToString());
                Assert.That(state.RootElement.GetProperty("maxPoints").GetInt32(), Is.EqualTo(entities.System<PersonalLoadoutSystem>().Points + (host ? 12 : 0)),
                    "An already-open editor must invalidate its donor access cache");
                Assert.That(HeadshotLimits.LibrarySlots(supporters.GetSupporterTier(local), false), Is.EqualTo(host ? 10 : 1));
            });
        }
        await SetFlags(AdminFlags.Admin, true); await Check(false);
        await SetFlags(AdminFlags.Host, true); await Check(true);
        await SetFlags(AdminFlags.Host, false); await Check(true);
        await SetFlags(AdminFlags.Admin, true); await Check(false);
        await pair.Client.WaitPost(() => editor.Dispose());
        await pair.CleanReturnAsync();
    }
}
