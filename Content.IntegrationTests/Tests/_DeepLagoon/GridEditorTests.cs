using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Content.Server.Administration.Managers;
using Content.Server.Decals;
using Content.Shared._DeepLagoon.Mapping;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using ClientEditor = Content.Client._DeepLagoon.Mapping.GridEditorSystem;
using ServerEditor = Content.Server._DeepLagoon.Mapping.GridEditorSystem;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture, NonParallelizable]
public sealed class GridEditorTests
{
    [Test]
    public async Task DraftAppliesTilesEntitiesAndDecalsAndRejectsStaleOrUnauthorizedEdits()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true, Connected = true });
        EntityUid grid = default, chair = default;
        string decalId = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: false);
            var created = pair.Server.ResolveDependency<IMapManager>().CreateGridEntity(mapId);
            grid = created.Owner;
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            entities.System<SharedMapSystem>().SetTile(grid, created.Comp, new Vector2i(0, 0), new Tile(tiles["Plating"].TileId));
            var session = pair.Server.PlayerMan.Sessions.Single();
            pair.Server.ResolveDependency<IAdminManager>().PromoteHost(session);
            chair = entities.SpawnEntity("ChairWood", new EntityCoordinates(grid, new Vector2(.5f, .5f)));
            entities.System<MetaDataSystem>().SetEntityName(chair, "Preserved mapper name");
            decalId = pair.Server.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<DecalPrototype>().First().ID;
        });
        await PoolManager.WaitUntil(pair.Server, () => Task.FromResult(pair.Server.ResolveDependency<IAdminManager>()
            .HasAdminFlag(pair.Server.PlayerMan.Sessions.Single(), AdminFlags.Mapping)));
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.System<ServerEditor>()
            .Open(pair.Server.PlayerMan.Sessions.Single(), grid), Does.Contain("открыт")));
        await pair.RunTicksSync(30);
        var action = typeof(ClientEditor).GetMethod("Action", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var options = new JsonSerializerOptions { IncludeFields = true };
        GridEditorItem Copy(GridEditorItem item) => JsonSerializer.Deserialize<GridEditorItem>(JsonSerializer.Serialize(item, options), options)!;
        string Payload(IEnumerable<GridEditorItem> items) => JsonSerializer.Serialize(new { items = items.Select(i => new
        { id = i.Id, kind = i.Kind, prototype = i.Prototype, x = i.X, y = i.Y, rotation = i.Rotation, color = i.Color,
          z = i.Z, cleanable = i.Cleanable, anchored = i.Anchored, variant = i.Variant, flags = i.Flags, mirror = i.Mirror }) });
        void Send(ClientEditor editor, IEnumerable<GridEditorItem> items, string purpose = "apply", string name = "")
        {
            var list = items.ToList();
            var transfer = Guid.NewGuid().ToString();
            var chunks = Math.Max(1, (list.Count + 15) / 16);
            action.Invoke(editor, new object[] { "applyStart", JsonSerializer.Serialize(new { id = transfer, count = list.Count, chunks, purpose, name }) });
            for (var index = 0; index < chunks; index++)
            {
                var batch = JsonDocument.Parse(Payload(list.Skip(index * 16).Take(16))).RootElement.GetProperty("items");
                var json = JsonSerializer.Serialize(new { id = transfer, index, items = batch });
                Assert.That(json.Length, Is.LessThan(8192), "Every chunk must fit the native bridge limit");
                action.Invoke(editor, new object[] { "applyChunk", json });
            }
        }
        Guid oldToken = default;
        Guid savedToken = default;
        await pair.Client.WaitAssertion(() =>
        {
            var editor = pair.Client.EntMan.System<ClientEditor>();
            Assert.That(editor.State, Is.Not.Null);
            oldToken = editor.State!.Token;
            var existing = Copy(editor.State.Items.Single(i => i.Prototype == "ChairWood"));
            var draft = new List<GridEditorItem> { existing, new() { Id = "t:0:0", Kind = "remove" } };
            existing.X = 6.5f; existing.Y = 6.5f; existing.Rotation = 90;
            draft.RemoveAll(i => i.Id == "t:6:6");
            draft.Add(new GridEditorItem { Id = "t:6:6", Kind = "tile", Prototype = "Plating", X = 6, Y = 6 });
            draft.Add(new GridEditorItem { Id = "new_decal", Kind = "decal", Prototype = decalId, X = 6, Y = 6 });
            Send(editor, draft);
        });
        await pair.RunTicksSync(30);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<TransformComponent>(chair).LocalPosition, Is.EqualTo(new Vector2(6.5f, 6.5f)));
            Assert.That(entities.GetComponent<MetaDataComponent>(chair).EntityName, Is.EqualTo("Preserved mapper name"));
            Assert.That(entities.System<SharedMapSystem>().GetTileRef(grid, entities.GetComponent<MapGridComponent>(grid), new Vector2i(6, 6)).Tile.IsEmpty, Is.False);
            Assert.That(entities.System<SharedMapSystem>().GetTileRef(grid, entities.GetComponent<MapGridComponent>(grid), new Vector2i(0, 0)).Tile.IsEmpty, Is.True);
            Assert.That(entities.System<DecalSystem>().GetDecalsInRange(grid, new Vector2(6, 6)), Is.Not.Empty);
        });
        await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.System<ClientEditor>().State!.Token, Is.Not.EqualTo(oldToken)));
        await pair.Client.WaitAssertion(() =>
        {
            var editor = pair.Client.EntMan.System<ClientEditor>();
            savedToken = editor.State!.Token;
            action.Invoke(editor, new object[] { "favorite", "{\"kind\":\"entity\",\"id\":\"WallSolid\"}" });
            Send(editor, editor.State.Items, "template", "Тестовая каюта");
            var file = pair.Client.ResolveDependency<IResourceManager>().UserData;
            Assert.That(file.Exists(new ResPath("/grid-editor/library.json")), Is.True);
            using var reader = file.OpenText(new ResPath("/grid-editor/library.json"));
            var json = JsonDocument.Parse(reader.ReadToEnd());
            Assert.That(json.RootElement.GetProperty("favorites").EnumerateArray().Any(f => f.GetProperty("id").GetString() == "WallSolid"), Is.True);
            Assert.That(json.RootElement.GetProperty("templates").EnumerateArray().Any(t => t.GetProperty("name").GetString() == "Тестовая каюта"), Is.True);
            // Read the saved file again through the same startup path used after restarting the client.
            typeof(ClientEditor).GetField("_libraryLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(editor, false);
            typeof(ClientEditor).GetMethod("LoadLibrary", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(editor, null);
        });
        await pair.Server.WaitAssertion(() =>
        {
            var editor = pair.Server.EntMan.System<ServerEditor>();
            var yaml = editor.Export(pair.Server.PlayerMan.Sessions.Single(), savedToken, false, out var error);
            Assert.That(yaml, Is.Not.Null, error);
            Assert.That(yaml, Does.Contain("ChairWood"));
            Assert.That(yaml, Does.Contain("Preserved mapper name"));
        });
        // An external mapper edit invalidates this snapshot; the user's draft must not overwrite it.
        await pair.Server.WaitAssertion(() => pair.Server.EntMan.System<SharedTransformSystem>().SetCoordinates(chair,
            new EntityCoordinates(grid, new Vector2(7.5f, 6.5f))));
        await pair.Client.WaitAssertion(() =>
        {
            var editor = pair.Client.EntMan.System<ClientEditor>();
            Send(editor, editor.State!.Items);
        });
        await pair.RunTicksSync(25);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.GetComponent<TransformComponent>(chair).LocalPosition.X, Is.EqualTo(7.5f));
            var session = pair.Server.PlayerMan.Sessions.Single();
            pair.Server.ResolveDependency<IAdminManager>().DeAdmin(session);
            Assert.That(pair.Server.EntMan.System<ServerEditor>().Open(session, grid), Does.Contain("Mapping"));
        });
        await pair.Client.WaitAssertion(() =>
        {
            var editor = pair.Client.EntMan.System<ClientEditor>();
            Send(editor, editor.State!.Items);
            action.Invoke(editor, new object[] { "close", "{}" });
        });
        await pair.RunTicksSync(25);
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.GetComponent<TransformComponent>(chair).LocalPosition.X, Is.EqualTo(7.5f)));
        await pair.CleanReturnAsync();
    }
}
