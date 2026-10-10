using System.Linq;
using System.Numerics;
using Content.Client._DeepLagoon.Spawn;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using EntitySpawnWindow = Content.Client._DeepLagoon.Spawn.EntitySpawnWindow;
using EntitySpawningUIController = Content.Client._DeepLagoon.Spawn.EntitySpawningUIController;
using TileSpawnWindow = Content.Client._DeepLagoon.Spawn.TileSpawnWindow;
using TileSpawningUIController = Content.Client._DeepLagoon.Spawn.TileSpawningUIController;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class FuzzySpawnWindowTests
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: FuzzySpawnVisibleTest
          name: квазиспавнокристалл
          suffix: тестовыйсуффиксквазиспавна

        - type: entity
          id: FuzzySpawnHiddenTest
          name: квазиспавнокристалл
          categories: [FuzzySpawnHiddenCategoryTest]

        - type: entity
          id: FuzzySpawnAbstractTest
          name: квазиспавнокристалл
          abstract: true

        - type: entityCategory
          id: FuzzySpawnHiddenCategoryTest
          hideSpawnMenu: true
        """;

    [Test]
    public async Task EntityWindowSearchAndPlacementUseContentController()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        EntitySpawnWindow window = default!;
        EntitySpawnButton button = default!;
        await pair.Client.WaitAssertion(() =>
        {
            pair.Client.ResolveDependency<IConfigurationManager>().SetCVar(CVars.EntitiesCategoryFilter, "");
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            ui.GetUIController<EntitySpawningUIController>().ToggleWindow();
            window = ui.WindowRoot.Children.OfType<EntitySpawnWindow>().Single();
            Assert.That(window.IsOpen, Is.True);
            // A transposition must find the visible prototype, but never hidden or abstract ones.
            window.SearchBar.SetText("квазиспавнокрситалл", true);
            Assert.That(window.PrototypeList.TotalItemCount, Is.EqualTo(1));
            button = window.PrototypeList.Children.OfType<EntitySpawnButton>().Single();
            Assert.That(button.PrototypeID, Is.EqualTo("FuzzySpawnVisibleTest"));
            window.SearchBar.SetText("тестовыйсуффиксквазиспавна", true);
            Assert.That(window.PrototypeList.TotalItemCount, Is.EqualTo(1));
            window.SearchBar.SetText("FuzzySpawnVisibleTest", true);
            button = window.PrototypeList.Children.OfType<EntitySpawnButton>().Single();
        });

        var control = button.ActualButton;
        var coordinates = new ScreenCoordinates(control.GlobalPixelPosition, control.Window?.Id ?? default);
        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
            await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(
                EngineKeyFunctions.UIClick, state, coordinates, default, Vector2.Zero, Vector2.Zero));

        await pair.Client.WaitAssertion(() =>
        {
            var placement = pair.Client.ResolveDependency<IPlacementManager>();
            Assert.That(placement.CurrentPermission?.EntityType, Is.EqualTo("FuzzySpawnVisibleTest"));
            Assert.That(placement.CurrentPermission!.IsTile, Is.False);
            window.SearchBar.SetText("", true);
            Assert.That(placement.CurrentPermission, Is.Null);
            Assert.That(window.PrototypeList.TotalItemCount, Is.GreaterThan(1));
            Assert.That(window.PrototypeList.ChildCount, Is.LessThan(window.PrototypeList.TotalItemCount),
                "The entity list must keep its virtualization after clearing search.");
            window.Close();
            Assert.That(placement.IsActive, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TileWindowSearchKeepsPlacementAndMirrorControls()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var controller = ui.GetUIController<TileSpawningUIController>();
            controller.ToggleWindow();
            var window = ui.WindowRoot.Children.OfType<TileSpawnWindow>().Single();
            var tiles = pair.Client.ResolveDependency<ITileDefinitionManager>();
            var tile = tiles.First(t => !t.EditorHidden && t.AllowRotationMirror);
            window.SearchBar.SetText(tile.ID, true);
            Assert.That(window.TileList.Count, Is.GreaterThan(0));
            // Exact ID search returns this tile first. Selection exercises the real list event.
            window.TileList[0].Selected = true;
            var placement = pair.Client.ResolveDependency<IPlacementManager>();
            Assert.That(placement.CurrentPermission?.TileType, Is.EqualTo(tile.TileId));
            Assert.That(placement.CurrentPermission!.IsTile, Is.True);
            Assert.That(window.MirroredButton.Disabled, Is.False);
            window.SearchBar.SetText("", true);
            Assert.That(placement.CurrentPermission, Is.Null);
            Assert.That(window.TileList.Count, Is.EqualTo(tiles.Count(t => !t.EditorHidden)));
            controller.CloseWindow();
            Assert.That(window.IsOpen, Is.False);
            controller.ToggleWindow();
            Assert.That(window.IsOpen, Is.True);
            controller.CloseWindow();
        });
        await pair.CleanReturnAsync();
    }
}
