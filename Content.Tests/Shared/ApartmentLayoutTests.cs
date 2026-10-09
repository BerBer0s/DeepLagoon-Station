#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._DeepLagoon.Apartments;
using NUnit.Framework;
using Robust.Shared.Maths;

namespace Content.Tests.Shared;

[TestFixture]
public sealed class ApartmentLayoutTests
{
    private static ApartmentTemplatePrototype Template() => new()
    {
        ID = "test", Width = 8, Height = 8, Catalog = new() { "chair", "table" },
        Furniture = new() { new() { Id = "base_chair", Furniture = "chair", X = 4, Y = 4 } },
    };
    private static ApartmentFurniturePrototype? Catalog(string id) => id switch
    {
        "chair" => new() { ID = id, MaxCount = 3, BlocksMovement = false },
        "table" => new() { ID = id, Size = new Vector2i(2, 1), MaxCount = 10 },
        _ => null,
    };

    [Test]
    public void PatchStoresOnlyFinalChangesAndRestoringBaselineCompactsIt()
    {
        var template = Template();
        var layout = template.Furniture.Select(p => p.Copy()).ToList();
        layout[0].X = 5;
        layout.Add(new() { Id = "added_table", Furniture = "table", X = 3, Y = 2 });
        var delta = ApartmentLayout.Diff(template, layout, 1);
        Assert.Multiple(() =>
        {
            Assert.That(delta.Changed, Has.Count.EqualTo(1));
            Assert.That(delta.Added, Has.Count.EqualTo(1));
            Assert.That(delta.Removed, Is.Empty);
            Assert.That(ApartmentLayout.Resolve(template, delta).Zip(layout).All(p => p.First.SameAs(p.Second)), Is.True);
        });
        var reset = ApartmentLayout.Diff(template, template.Furniture, 2);
        Assert.That(reset.Changed.Concat(reset.Added), Is.Empty);
    }

    [Test]
    public void RemovedBaselineDoesNotRespawnAndAddedIdsCannotImpersonateIt()
    {
        var template = Template();
        var delta = ApartmentLayout.Diff(template, Array.Empty<ApartmentPlacement>(), 1);
        Assert.That(delta.Removed, Is.EqualTo(new[] { "base_chair" }));
        Assert.That(ApartmentLayout.Resolve(template, delta), Is.Empty);
        delta.Added.Add(template.Furniture[0].Copy());
        Assert.Throws<InvalidOperationException>(() => ApartmentLayout.Resolve(template, delta));
    }

    [Test]
    public void TemplateVersionMismatchRequiresMigration()
    {
        var template = Template();
        var delta = ApartmentLayout.Diff(template, template.Furniture, 1);
        template.Version++;
        Assert.Throws<InvalidOperationException>(() => ApartmentLayout.Resolve(template, delta));
        Assert.That(delta.TemplateVersion, Is.EqualTo(1));
    }

    [Test]
    public void RotationBoundsOverlapExitAndCountAreValidated()
    {
        var template = Template();
        var a = new ApartmentPlacement { Id = "a", Furniture = "table", X = 6, Y = 4 };
        Assert.That(ApartmentLayout.Validate(template, new[] { a }, Catalog), Does.Contain("стеной"));
        a.Rotation = 1;
        Assert.That(ApartmentLayout.Validate(template, new[] { a }, Catalog), Is.Null);
        a.X = 1; a.Y = 1;
        Assert.That(ApartmentLayout.Validate(template, new[] { a }, Catalog), Does.Contain("прибытия"));
        a.X = 4; a.Y = 4;
        Assert.That(ApartmentLayout.Validate(template, new[] { a, template.Furniture[0] }, Catalog), Does.Contain("пересекаются"));
        var chairs = Enumerable.Range(0, 4).Select(i => new ApartmentPlacement { Id = $"chair_{i}", Furniture = "chair", X = 2 + i, Y = 2 }).ToArray();
        Assert.That(ApartmentLayout.Validate(template, chairs, Catalog), Does.Contain("экземпляров"));
    }

    [Test]
    public void FreeFloorCannotBeCutOffFromExit()
    {
        var template = Template();
        var wall = Enumerable.Range(1, 6).Select(y => new ApartmentPlacement { Id = $"table_{y}", Furniture = "table", X = 3, Y = y }).ToArray();
        Assert.That(ApartmentLayout.Validate(template, wall, Catalog), Does.Contain("проход"));
    }

    [Test]
    public void ArbitraryPrototypeAndBaselineReplacementAreRejected()
    {
        var template = Template();
        var replacement = template.Furniture[0].Copy(); replacement.Furniture = "table";
        Assert.That(ApartmentLayout.Validate(template, new[] { replacement }, Catalog), Does.Contain("подменить"));
        replacement.Id = "new"; replacement.Furniture = "ExplosiveBanana";
        Assert.That(ApartmentLayout.Validate(template, new[] { replacement }, Catalog), Does.Contain("каталоге"));
    }
}
