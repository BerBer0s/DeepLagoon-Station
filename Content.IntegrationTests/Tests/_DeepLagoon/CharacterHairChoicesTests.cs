using System.Linq;
using System.Reflection;
using System.Text.Json;
using Content.Client._Mono.Company;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture]
public sealed class CharacterHairChoicesTests
{
    [TestCase("Human", true)]
    [TestCase("Diona", false)]
    public async Task EditorHairChoicesRespectSpecies(string species, bool hasStyles)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            using var editor = new HumanoidProfileEditor(
                client.ResolveDependency<IClientPreferencesManager>(),
                client.ResolveDependency<IConfigurationManager>(),
                client.ResolveDependency<IEntityManager>(),
                client.ResolveDependency<IFileDialogManager>(),
                client.ResolveDependency<ILogManager>(),
                client.ResolveDependency<IPlayerManager>(),
                client.ResolveDependency<IPrototypeManager>(),
                client.ResolveDependency<IResourceCache>(),
                client.ResolveDependency<JobRequirementsManager>(),
                client.ResolveDependency<MarkingManager>(),
                client.ResolveDependency<CompanyManager>());
            editor.SetProfile(HumanoidCharacterProfile.DefaultWithSpecies(species), 0);
            using var state = JsonDocument.Parse(editor.CreateTguiProfileState("appearance").ToString());
            foreach (var key in new[] { "hairOptions", "beardOptions" })
            {
                var choices = state.RootElement.GetProperty(key).EnumerateArray().ToArray();
                Assert.That(choices.Length, hasStyles ? Is.GreaterThan(4) : Is.EqualTo(1), key);
                Assert.That(choices.Skip(1).All(choice => choice.GetProperty("iconId").GetString() == "marking/" + choice.GetProperty("id").GetString()), Is.True, key);
                Assert.That(choices.All(choice => !choice.TryGetProperty("images", out _)), Is.True, "Catalogue must not resend PNGs on profile edits");
            }
            var draft = editor.Profile;
            typeof(HumanoidProfileEditor).GetMethod("RestoreNativeEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
            Assert.That(editor.Profile, Is.SameAs(draft), "Native recovery must preserve the current draft");
            Assert.That(typeof(HumanoidProfileEditor).GetField("_tguiHost", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(editor), Is.Null);
            var tabs = editor.Children.SelectMany(child => child.Children).OfType<Robust.Client.UserInterface.Controls.TabContainer>().FirstOrDefault();
            Assert.That(tabs, Is.Not.Null, "Native tabs must return to the editor column");
            Assert.That(tabs!.Visible, Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
