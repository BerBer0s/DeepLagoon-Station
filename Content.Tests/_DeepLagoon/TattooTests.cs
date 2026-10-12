using System;
using System.Linq;
using System.Text.Json;
using Content.Client._DeepLagoon.Tattoos;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.Tattoos;
using Content.Shared.Humanoid;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class TattooTests
{
    public static string Bitmap(bool luminous = false)
    {
        using var image = new Image<Rgba32>(Tattoo.Resolution, Tattoo.Resolution);
        for (var y = 80; y < 160; y++) for (var x = 30; x < 90; x++) image[x, y] = luminous ? new Rgba32(0, 255, 0, 255) : new Rgba32(255, 0, 0, 255);
        return TattooSpriteFrames.Png(image).Split(',')[1];
    }

    [Test]
    public void FinishingFlattensHistoryAndKeepsBothInkChannelsAndPlacement()
    {
        var original = Drawing(); original.Scale = .7f;
        var normal = Bitmap(); var glow = Bitmap(true);
        Assert.That(TattooBitmap.ValidPng(normal), Is.True);
        Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(new { id = original.Id, normal, glow }), out var args, Tattoo.MaxBakePayload), Is.True);
        Assert.That(TattooEditorData.TryApply("tattoo-bake", args!, [original], out var next, out _), Is.True);
        var baked = next.Single();
        Assert.That(baked.Strokes, Is.Empty); Assert.That(original.Strokes, Has.Count.EqualTo(1));
        Assert.That(baked.NormalBitmap, Is.EqualTo(normal)); Assert.That(baked.GlowBitmap, Is.EqualTo(glow));
        Assert.That(baked.Scale, Is.EqualTo(.7f));
        Assert.That(TattooEditorData.TryApply("tattoo-undo", args!, next, out _, out _), Is.False);
        var restored = JsonSerializer.Deserialize<Tattoo>(JsonSerializer.Serialize(baked))!;
        Assert.That(restored.ContentEquals(baked), Is.True);
        Assert.That(JsonSerializer.Serialize(baked), Does.Not.Contain("Layers"));
    }

    [Test]
    public void BakedArtworkStaysMaskedAndMirrorsWithoutChangingSavedPixels()
    {
        var tattoo = new Tattoo { NormalBitmap = Bitmap(), Mirrored = true, Scale = 1.3f, Rotation = 20 };
        using var mask = new Image<Rgba32>(32, 32);
        for (var y = 8; y < 24; y++) for (var x = 8; x < 24; x++) mask[x,y] = new Rgba32(255,255,255,255);
        using var output = TattooRasterizer.Render(tattoo, false, mask);
        var count = 0;
        for (var y = 0; y < output.Height; y++) for (var x = 0; x < output.Width; x++)
        { if (mask[x * 32 / output.Width, y * 32 / output.Height].A == 0) Assert.That(output[x,y].A, Is.Zero); else if(output[x,y].A > 0) count++; }
        Assert.That(count, Is.GreaterThan(0));
        Assert.That(tattoo.NormalBitmap, Is.EqualTo(Bitmap()));
    }

    [Test]
    public void BakeChunksAreBoundedOrderedAndBoundToTheirTattoo()
    {
        var editor = new TattooEditorData(); var tattoo = Drawing();
        var payload = JsonSerializer.Serialize(new { id = tattoo.Id, normal = Bitmap(), glow = "" });
        var chunks = (int)Math.Ceiling(payload.Length / 100.0);
        TguiActionData completed = null;
        for (var i = 0; i < chunks; i++)
        {
            Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(new { id = tattoo.Id, index = i, count = chunks, text = payload.Substring(i * 100, Math.Min(100, payload.Length - i * 100)) }), out var args), Is.True);
            Assert.That(editor.ReceiveBakeChunk(args!, out completed), Is.True);
            if (i < chunks - 1) Assert.That(completed, Is.Null);
        }
        Assert.That(completed, Is.Not.Null);
        Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(new { id = tattoo.Id, index = 1, count = 2, text = "{}" }), out var invalid), Is.True);
        Assert.That(editor.ReceiveBakeChunk(invalid!, out _), Is.False);
        Assert.That(TattooBitmap.ValidPng(Convert.ToBase64String(new byte[100])), Is.False);
        Assert.That(TattooBitmap.ValidPng(new string('A', Tattoo.MaxBitmapChars + 1)), Is.False);
    }
    public static Tattoo Drawing() => new()
    {
        Strokes = [new TattooStroke { Width = .1f, Color = "#FF0000FF", Points = [new(.25f, .5f), new(.75f, .5f)] }],
    };

    [Test]
    public void InkIsClippedAfterScalingAndPlacement()
    {
        using var mask = new Image<Rgba32>(32, 32);
        for (var y = 10; y < 22; y++) for (var x = 10; x < 22; x++) mask[x, y] = new Rgba32(255, 255, 255, 255);
        var tattoo = Drawing(); tattoo.Scale = 3; tattoo.Rotation = 37; tattoo.X = .6f;
        using var ink = TattooRasterizer.Render(tattoo, false, mask);
        var visible = 0;
        for (var y = 0; y < ink.Height; y++) for (var x = 0; x < ink.Width; x++)
        {
            if (mask[x * 32 / ink.Width, y * 32 / ink.Height].A == 0)
                Assert.That(ink[x, y].A, Is.Zero, $"Ink leaked at {x},{y}");
            else if (ink[x, y].A != 0) visible++;
        }
        Assert.That(visible, Is.GreaterThan(0));
    }

    [Test]
    public void OrdinaryAndLuminousStrokesRemainSeparateAndEraserRemovesBoth()
    {
        using var mask = new Image<Rgba32>(32, 32, new Rgba32(255, 255, 255, 255));
        var tattoo = Drawing();
        tattoo.Strokes.Add(new TattooStroke { Glow = true, Width = .1f, Color = "#00FF00FF", Points = [new(.5f, .25f), new(.5f, .75f)] });
        using (var normal = TattooRasterizer.Render(tattoo, false, mask))
        using (var glow = TattooRasterizer.Render(tattoo, true, mask))
        {
            Assert.That(normal[80, 128].R, Is.EqualTo(255));
            Assert.That(glow[80, 128].A, Is.Zero);
            Assert.That(normal[128, 80].A, Is.Zero);
            Assert.That(glow[128, 80].G, Is.EqualTo(255));
        }
        tattoo.Strokes.Add(new TattooStroke { Erase = true, Width = .2f, Points = [new(.5f, .5f)] });
        using var erasedNormal = TattooRasterizer.Render(tattoo, false, mask);
        using var erasedGlow = TattooRasterizer.Render(tattoo, true, mask);
        Assert.That(erasedNormal[128, 128].A, Is.Zero);
        Assert.That(erasedGlow[128, 128].A, Is.Zero);
    }

    [Test]
    public void AntialiasingDoesNotQuantizeInkToBodyPixels()
    {
        using var mask = new Image<Rgba32>(32, 32, new Rgba32(255, 255, 255, 255));
        var tattoo = Drawing(); tattoo.Strokes[0].Width = .012f;
        tattoo.Strokes[0].Points = [new(.23f, .19f), new(.79f, .83f)];
        using var ink = TattooRasterizer.Render(tattoo, false, mask);
        var smooth = false;
        for (var y = 0; y < ink.Height; y++) for (var x = 0; x < ink.Width; x++)
            smooth |= ink[x, y].A is > 0 and < 255;
        Assert.That(ink.Width, Is.EqualTo(Tattoo.Resolution));
        Assert.That(smooth, Is.True);
    }

    [Test]
    public void ProfileAppearanceEditsAndCopiesPreserveIndependentTattooData()
    {
        var appearance = new HumanoidCharacterAppearance { Tattoos = [Drawing()] };
        var changed = appearance.WithHairColor(Robust.Shared.Maths.Color.Blue).WithHeight(1.1f).WithWidth(.9f).WithSkinColor(Robust.Shared.Maths.Color.White);
        Assert.That(changed.Tattoos.Single().ContentEquals(appearance.Tattoos.Single()), Is.True);
        changed.Tattoos[0].Strokes[0].Points[0] = new(.1f, .1f);
        Assert.That(appearance.Tattoos[0].Strokes[0].Points[0].X, Is.EqualTo(.25f));
        Assert.That(appearance.MemberwiseEquals(changed), Is.False);
    }

    [Test]
    public void DatabaseJsonRoundTripPreservesGlowAndPlacement()
    {
        var tattoo = Drawing(); tattoo.Direction = 3; tattoo.Scale = .4f; tattoo.Strokes[0].Glow = true;
        var json = JsonSerializer.Serialize(new[] { tattoo });
        var restored = JsonSerializer.Deserialize<Tattoo[]>(json)![0];
        Assert.That(restored.ContentEquals(tattoo), Is.True);
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1)]
    [TestCase(10)]
    public void InvalidScaleCannotEnterTheProfile(float scale)
    {
        var tattoo = Drawing(); tattoo.Scale = scale;
        Assert.That(Tattoo.Sanitize([tattoo]), Is.Empty);
    }

    [Test]
    public void OversizedStrokeAndUnsupportedBodyRegionAreRejected()
    {
        var tattoo = Drawing(); tattoo.Strokes[0].Points = Enumerable.Repeat(new TattooPoint(.5f, .5f), Tattoo.MaxStrokePoints + 1).ToList();
        Assert.That(tattoo.IsValid(), Is.False);
        tattoo = Drawing(); tattoo.BodyPart = HumanoidVisualLayers.Hair;
        Assert.That(tattoo.IsValid(), Is.False);
    }

    [Test]
    public void TguiDrawingAndUndoDoNotMutateThePublishedDraft()
    {
        var tattoo = Drawing();
        Assert.That(TguiActionData.TryParse(JsonSerializer.Serialize(new { id = tattoo.Id, color = "#44FF88FF", width = .02f, glow = true, erase = false, points = ".3,.3;.7,.7" }), out var action), Is.True);
        Assert.That(TattooEditorData.TryApply("tattoo-stroke", action!, [tattoo], out var next, out _), Is.True);
        Assert.That(tattoo.Strokes.Count, Is.EqualTo(1));
        Assert.That(next[0].Strokes.Count, Is.EqualTo(2));
        Assert.That(next[0].Strokes[1].Glow, Is.True);
        Assert.That(TattooEditorData.TryApply("tattoo-undo", action!, next, out var undone, out _), Is.True);
        Assert.That(undone[0].Strokes.Count, Is.EqualTo(1));
    }
}
