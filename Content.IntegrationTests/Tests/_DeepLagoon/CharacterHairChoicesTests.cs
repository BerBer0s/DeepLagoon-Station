using System.Linq;
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
                Assert.That(choices.Skip(1).All(choice => choice.GetProperty("images")[0].GetProperty("url").GetString()!.StartsWith("data:image/png;base64,")), Is.True, key);
            }
        });
        await pair.CleanReturnAsync();
    }
}
