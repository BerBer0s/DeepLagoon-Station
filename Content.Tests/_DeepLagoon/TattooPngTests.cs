using System;
using System.IO;
using Content.Client._DeepLagoon.Tattoos;
using Content.Shared._DeepLagoon.Tattoos;
using Content.Shared.Humanoid;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class TattooPngTests
{
    [Test]
    public void ExportIsAReadablePngAndRoundTripPreservesBothChannelsExactly()
    {
        var source = new Tattoo { NormalBitmap = TattooTests.Bitmap(), GlowBitmap = TattooTests.Bitmap(true), Mirrored = true, Scale = .3f };
        var file = TattooPng.Export(source);
        using var stream = new MemoryStream(file); using var visible = Image.Load<Rgba32>(stream);
        Assert.That(visible.Width, Is.EqualTo(256)); Assert.That(visible.Height, Is.EqualTo(256));
        Assert.That(visible[40,100].G, Is.EqualTo(255));
        var target = new Tattoo { BodyPart = HumanoidVisualLayers.LArm, Direction = 2, X = .7f, Scale = .8f };
        var imported = TattooPng.Import(file, target);
        Assert.That(imported.NormalBitmap, Is.EqualTo(source.NormalBitmap));
        Assert.That(imported.GlowBitmap, Is.EqualTo(source.GlowBitmap));
        Assert.That(imported.Id, Is.EqualTo(target.Id)); Assert.That(imported.BodyPart, Is.EqualTo(target.BodyPart));
        Assert.That(imported.X, Is.EqualTo(target.X)); Assert.That(imported.Scale, Is.EqualTo(target.Scale));
        Assert.That(imported.Strokes, Is.Empty);
    }

    [Test]
    public void OrdinaryPngIsResizedWithoutDistortingItsAspectRatioAndHasNoGlow()
    {
        using var original = new Image<Rgba32>(32, 16, new Rgba32(255, 0, 0, 255));
        var bytes = Convert.FromBase64String(TattooSpriteFrames.Png(original).Split(',')[1]);
        var imported = TattooPng.Import(bytes, new Tattoo(), allowExternalPng: true);
        Assert.That(imported.IsValid(), Is.True); Assert.That(imported.GlowBitmap, Is.Empty);
        using var stream = new MemoryStream(Convert.FromBase64String(imported.NormalBitmap));
        using var normalized = Image.Load<Rgba32>(stream);
        Assert.That(normalized[128, 0].A, Is.Zero); Assert.That(normalized[128, 128].R, Is.EqualTo(255));
        Assert.That(normalized[128, 255].A, Is.Zero);
    }

    [Test]
    public void ExportPreservesAnAbsentGlowChannelAndConvertsLegacyStrokes()
    {
        var source = new Tattoo { NormalBitmap = TattooTests.Bitmap() };
        Assert.That(TattooPng.Import(TattooPng.Export(source), new Tattoo()).GlowBitmap, Is.Empty);
        var legacy = TattooTests.Drawing();
        var imported = TattooPng.Import(TattooPng.Export(legacy), new Tattoo());
        Assert.That(imported.NormalBitmap, Is.Not.Empty); Assert.That(imported.Strokes, Is.Empty);
        Assert.That(legacy.Strokes, Has.Count.EqualTo(1));
    }

    [Test]
    public void CorruptOversizedOrTruncatedFilesCannotReplaceAnExistingDrawing()
    {
        var target = new Tattoo { NormalBitmap = TattooTests.Bitmap() };
        var original = target.NormalBitmap;
        var png = TattooPng.Export(target); png[40] ^= 1;
        Assert.Throws<InvalidDataException>(() => TattooPng.Import(png, target));
        Assert.Throws<InvalidDataException>(() => TattooPng.Import(new byte[TattooPng.MaxFileBytes + 1], target));
        Assert.Throws<InvalidDataException>(() => TattooPng.Import(TattooPng.Export(target)[..^10], target));
        Assert.That(target.NormalBitmap, Is.EqualTo(original));
    }

    [TestCase(0, false)]
    [TestCase(1, false)]
    [TestCase(2, false)]
    [TestCase(3, true)]
    public void OnlyThirdSupporterTierCanImportExternalPng(int tier, bool allowed)
    {
        Assert.That(TattooImportPolicy.CanImportExternalPng(tier), Is.EqualTo(allowed));
        using var image = new Image<Rgba32>(32, 32, new Rgba32(255, 0, 0, 255));
        var file = Convert.FromBase64String(TattooSpriteFrames.Png(image).Split(',')[1]);
        var template = new Tattoo { NormalBitmap = TattooTests.Bitmap() };
        if (allowed) Assert.That(TattooPng.Import(file, template, allowed).IsValid(), Is.True);
        else Assert.Throws<InvalidDataException>(() => TattooPng.Import(file, template, allowed));
        // Native exports remain portable without any donor entitlement.
        Assert.That(TattooPng.Import(TattooPng.Export(template), new Tattoo(), false).NormalBitmap, Is.EqualTo(template.NormalBitmap));
    }
}
