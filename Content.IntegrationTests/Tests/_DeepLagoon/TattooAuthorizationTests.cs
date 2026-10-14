using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Shared._DeepLagoon.Tattoos;
using Content.Shared.Preferences;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class TattooAuthorizationTests
{
    [Test]
    public async Task ForgedImportIsRejectedOverGameChannelAndCannotEnterPreferences()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        await pair.Server.WaitPost(() =>
        {
            var session = pair.Server.PlayerMan.Sessions.Single();
            if (pair.Server.ResolveDependency<Content.Server.Administration.Managers.IAdminManager>().GetAdminData(session, true) is { } admin)
                admin.Flags &= ~Content.Shared.Administration.AdminFlags.Host;
        });
        var forged = new Tattoo { NormalBitmap = Content.Tests._DeepLagoon.TattooTests.Bitmap(),
            ArtworkProof = Guid.NewGuid().ToString("N") + ":" + new string('0', 64) };
        Task<Tattoo> pending = null;
        await pair.Client.WaitPost(() => pending = pair.Client.EntMan.System<Content.Client._DeepLagoon.Tattoos.TattooArtworkSystem>().Authorize(forged));
        await pair.RunTicksSync(100);
        Assert.That(pending.IsFaulted, Is.True);
        Assert.That(pending.Exception!.GetBaseException().Message, Does.Contain("третий тир"));
        Task save = Task.CompletedTask;
        NetUserId user = default; int slot = 0;
        await pair.Server.WaitPost(() =>
        {
            var preferences = pair.Server.ResolveDependency<IServerPreferencesManager>();
            user = pair.Server.PlayerMan.Sessions.Single().UserId;
            slot = preferences.GetPreferences(user).SelectedCharacterIndex;
            var profile = (HumanoidCharacterProfile)preferences.GetPreferences(user).GetProfile(slot);
            var appearance = profile.Appearance.Clone(); appearance.Tattoos = [forged];
            save = preferences.SetProfile(user, slot, profile.WithCharacterAppearance(appearance), authoritative: false, persist: false);
        });
        await pair.RunTicksSync(20); await save;
        await pair.Server.WaitAssertion(() =>
        {
            var profile = (HumanoidCharacterProfile)pair.Server.ResolveDependency<IServerPreferencesManager>().GetPreferences(user).GetProfile(slot);
            Assert.That(profile.Appearance.Tattoos.Any(t => t.NormalBitmap == forged.NormalBitmap), Is.False,
                "Bypassing the importer UI and sending profile bitmap data must still fail");
        });
        await pair.CleanReturnAsync();
    }
}
