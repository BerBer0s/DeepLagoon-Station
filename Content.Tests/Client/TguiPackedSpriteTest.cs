using System;
using System.IO;
using Content.Client._DeepLagoon.WebUI;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.Client;

[TestFixture]
public sealed class TguiPackedSpriteTest
{
    [TestCase("\"directions\":4", 4)]
    [TestCase("\"directions\":4,\"delays\":[[0.1,0.2],[0.1],[0.1],[0.1]]", 5)]
    [TestCase("\"directions\":1,\"delays\":[[]]", 1)]
    public void PackedImagesUseFileCoordinatesAcrossStatesAndAtlasRows(string firstState, int offset)
    {
        using var atlas = new Image<Rgba32>(8, 8);
        var expected = new Rgba32(19, 75, 143, 255);
        for (var y = 0; y < 2; y++)
        for (var x = 0; x < 2; x++)
            atlas[offset % 4 * 2 + x, offset / 4 * 2 + y] = expected;
        atlas.Metadata.GetPngMetadata().TextData.Add(new PngTextData("robusttoolbox_rsic_meta",
            "{\"size\":{\"x\":2,\"y\":2},\"states\":[{\"name\":\"animated\"," + firstState +
            "},{\"name\":\"hair\",\"directions\":4}]}", "", ""));
        using var packed = new MemoryStream();
        atlas.SaveAsPng(packed);
        packed.Position = 0;
        using var deployed = Image.Load<Rgba32>(packed);
        var url = TguiSpriteImages.PackedFrame(deployed, "hair");
        using var result = Image.Load<Rgba32>(Convert.FromBase64String(url.Split(',')[1]));
        Assert.Multiple(() =>
        {
            Assert.That(result.Width, Is.EqualTo(2));
            Assert.That(result.Height, Is.EqualTo(2));
            Assert.That(result[0, 0], Is.EqualTo(expected));
            Assert.That(result[1, 1], Is.EqualTo(expected));
        });
    }

    [Test]
    public void UnknownStateCannotReturnAnotherItemsImage()
    {
        using var atlas = new Image<Rgba32>(2, 2);
        atlas.Metadata.GetPngMetadata().TextData.Add(new PngTextData("robusttoolbox_rsic_meta",
            "{\"size\":{\"x\":2,\"y\":2},\"states\":[{\"name\":\"item\"}]}", "", ""));
        Assert.Throws<InvalidDataException>(() => TguiSpriteImages.PackedFrame(atlas, "missing"));
    }
}
