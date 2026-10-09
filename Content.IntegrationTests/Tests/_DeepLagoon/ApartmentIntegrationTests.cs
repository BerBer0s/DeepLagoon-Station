using System.Linq;
using System.Collections.Generic;
using Content.Client._DeepLagoon.Apartments;
using Content.Server._DeepLagoon.Apartments;
using Content.Shared._DeepLagoon.Apartments;
using Content.Shared.Inventory;
using Content.Shared.Light.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared._CE.ZLevels.Core.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Enums;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class ApartmentIntegrationTests
{
    [Test]
    public async Task PreviewApplyEscrowPersistenceAndFreezeRunThroughConnectedClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, Connected = true });
        var testMap = await pair.CreateTestMap();
        EntityUid body = default;
        EntityUid originalUniform = default;
        EntityUid grid = default;
        NetEntity originalSofa = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var config = pair.Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(ApartmentCVars.Enabled, true);
            body = entities.SpawnEntity("MobHuman", testMap.GridCoords);
            var session = pair.Server.PlayerMan.Sessions.Single();
            pair.Server.PlayerMan.SetAttachedEntity(session, body);
            originalUniform = entities.SpawnEntity("ClothingUniformJumpsuitColorBlue", testMap.GridCoords);
            Assert.That(entities.System<InventorySystem>().TryEquip(body, originalUniform, "jumpsuit", force: true), Is.True);
            var system = entities.System<ApartmentSystem>();
            Assert.That(system.Enter(session, session.UserId, out var message), Is.True, message);
            Assert.That(entities.GetComponent<TransformComponent>(originalUniform).MapID, Is.Not.EqualTo(entities.GetComponent<TransformComponent>(body).MapID));
            Assert.That(entities.GetComponent<MetaDataComponent>(originalUniform).EntityPaused, Is.True);
            grid = system.Instances[session.UserId].Grid;
            originalSofa = entities.GetNetEntity(system.Instances[session.UserId].Furniture["base_sofa_01"]);
        });
        await pair.RunTicksSync(35);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<GridAtmosphereComponent>(grid), Is.False);
            var air = entities.System<AtmosphereSystem>().GetContainingMixture(body);
            Assert.That(air, Is.Not.Null);
            Assert.That(air!.Immutable, Is.True);
            Assert.That(air.GetMoles(Gas.Oxygen), Is.GreaterThan(20));
            Assert.That(air.Pressure, Is.InRange(90, 110));
        });
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.System<ApartmentSystem>().OpenEditor(pair.Server.PlayerMan.Sessions.Single()), Is.True));
        await pair.RunTicksSync(35);
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var editor = entities.System<ApartmentEditorSystem>();
            var clientBody = pair.Client.PlayerMan.LocalEntity!.Value;
            Assert.That(entities.HasComponent<CEZLevelViewerComponent>(clientBody), Is.True);
            var clientMap = entities.GetComponent<TransformComponent>(clientBody).MapUid;
            Assert.That(clientMap, Is.Not.Null);
            Assert.That(entities.GetComponent<MapLightComponent>(clientMap!.Value).AmbientLightColor, Is.EqualTo(Color.White));
            Assert.That(entities.GetComponent<ImplicitRoofComponent>(entities.GetComponent<TransformComponent>(clientBody).GridUid!.Value).Color, Is.EqualTo(Color.White));
            Assert.That(editor.IsOpen, Is.True);
            Assert.That(editor.Draft, Has.Count.EqualTo(6));
            Assert.That(entities.GetComponent<SpriteComponent>(entities.GetEntity(originalSofa)).Visible, Is.False);
            // Client draft is isolated until an explicit network apply.
            editor.Draft.Single(p => p.Id == "base_sofa_01").X = 3;
        });
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.System<ApartmentSystem>()
            .Instances[pair.Server.PlayerMan.Sessions.Single().UserId].Delta.Changed, Is.Empty));
        // Exercise the same panel action as the Apply button, including its sandbox parser.
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var editor = entities.System<ApartmentEditorSystem>();
            var ui = pair.Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>();
            var window = ui.WindowRoot.Children.OfType<ApartmentEditorWindow>().Single();
            // Panel actions route through a private handler; invoke it with the actual bounded JSON payload.
            var method = typeof(ApartmentEditorSystem).GetMethod("OnAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var layout = editor.Draft.Select(p => new { id = p.Id, furniture = p.Furniture, x = p.X, y = p.Y, rotation = p.Rotation });
            method.Invoke(editor, new object[] { "apply", System.Text.Json.JsonSerializer.Serialize(new { layout }) });
        });
        await pair.RunTicksSync(35);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var session = pair.Server.PlayerMan.Sessions.Single();
            var system = entities.System<ApartmentSystem>();
            var instance = system.Instances[session.UserId];
            Assert.That(instance.Delta.Changed, Has.Count.EqualTo(1));
            Assert.That(instance.Delta.Changed[0].X, Is.EqualTo(3));
            Assert.That(instance.Delta.Added, Is.Empty);
            var sofa = instance.Furniture["base_sofa_01"];
            Assert.That(entities.GetComponent<TransformComponent>(sofa).LocalPosition.X, Is.EqualTo(3.5f));
            // Reject a second request from the old draft revision before mutating anything.
            Assert.That(system.Apply(session, Guid.Empty, 0, new List<ApartmentPlacement>()), Does.Contain("доступа"));
            // Losing the attached participant must freeze the entire room even
            // while their body and deposited inventory remain in the instance.
            pair.Server.PlayerMan.SetAttachedEntity(session, null);
            system.RefreshActivity(instance);
            Assert.That(instance.Frozen, Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(body).EntityPaused, Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(sofa).EntityPaused, Is.True);
            pair.Server.PlayerMan.SetAttachedEntity(session, body);
            system.RefreshActivity(instance);
            Assert.That(instance.Frozen, Is.False);
            Assert.That(entities.GetComponent<MetaDataComponent>(body).EntityPaused, Is.False);
            Assert.That(system.Leave(session.UserId, out var message), Is.True, message);
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(body, "jumpsuit", out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(originalUniform));
            Assert.That(entities.GetComponent<MetaDataComponent>(originalUniform).EntityPaused, Is.False);
            Assert.That(instance.Frozen, Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(grid).EntityPaused, Is.True);
            Assert.That(entities.GetComponent<MetaDataComponent>(sofa).EntityPaused, Is.True);
            Assert.That(system.Enter(session, session.UserId, out message), Is.True, message);
            Assert.That(instance.Frozen, Is.False);
            Assert.That(entities.GetComponent<MetaDataComponent>(grid).EntityPaused, Is.False);
            Assert.That(entities.GetComponent<MetaDataComponent>(sofa).EntityPaused, Is.False);
            Assert.That(system.Leave(session.UserId, out message), Is.True, message);
            pair.Server.PlayerMan.SetAttachedEntity(session, null);
            entities.DeleteEntity(body);
        });
        await pair.RunTicksSync(15);
        await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.System<ApartmentEditorSystem>().IsOpen, Is.False));
        await pair.CleanReturnAsync();
    }
}
