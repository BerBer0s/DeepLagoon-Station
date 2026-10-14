using System.Collections.Generic;
using System.Reflection;
using Content.Shared.Entry;
using Content.Shared.Maps;
using Moq;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Shared;

[TestFixture]
public sealed class TilePrototypeReloadTests
{
    private static ContentTileDefinition Tile(string name, ushort tileId = 0)
    {
        var definition = new ContentTileDefinition();
        typeof(ContentTileDefinition).GetProperty(nameof(ContentTileDefinition.ID))!.SetValue(definition, name);
        definition.AssignTileId(tileId);
        return definition;
    }

    private static ITileDefinitionManager Registry(params ContentTileDefinition[] definitions)
    {
        var registry = new Mock<ITileDefinitionManager>(MockBehavior.Strict);
        var byName = new Dictionary<string, ITileDefinition>();
        foreach (var definition in definitions) byName.Add(definition.ID, definition);
        registry.Setup(manager => manager.TryGetDefinition(It.IsAny<string>(), out It.Ref<ITileDefinition>.IsAny))
            .Returns((string name, out ITileDefinition definition) => byName.TryGetValue(name, out definition));
        return registry.Object;
    }

    [Test]
    public void NewTileDoesNotThrowOrPreventKnownTilesFromRecoveringTheirIds()
    {
        var space = Tile("Space");
        var plating = Tile("Plating");
        var diagonal = Tile("DLPlatingDiagonal");
        var registry = Registry(Tile("Space", 0), Tile("Plating", 42));
        var missing = TilePrototypeReload.RestoreIds(registry, new[] { diagonal, space, plating });
        Assert.Multiple(() =>
        {
            Assert.That(missing, Is.EqualTo(new[] { "DLPlatingDiagonal" }));
            Assert.That(plating.TileId, Is.EqualTo(42));
            Assert.That(space.TileId, Is.Zero);
            Assert.That(diagonal.TileId, Is.Zero);
        });
    }

    [Test]
    public void ReloadKeepsExistingNumericIdsRegardlessOfPrototypeOrder()
    {
        var first = Tile("Z", 999);
        var second = Tile("A", 999);
        var registry = Registry(Tile("Z", 3), Tile("A", 17));
        Assert.That(TilePrototypeReload.RestoreIds(registry, new[] { second, first }), Is.Empty);
        Assert.That(first.TileId, Is.EqualTo(3));
        Assert.That(second.TileId, Is.EqualTo(17));
    }

    [Test]
    public void RemovedPrototypeDoesNotRenumberRemainingTiles()
    {
        var retained = Tile("Z");
        var registry = Registry(Tile("A", 1), Tile("Z", 2));
        Assert.That(TilePrototypeReload.RestoreIds(registry, new[] { retained }), Is.Empty);
        Assert.That(retained.TileId, Is.EqualTo(2));
    }
}
