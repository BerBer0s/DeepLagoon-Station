using System.IO;
using System.Text;
using Content.Server._DeepLagoon.CharacterInfo;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class HeadshotPrivacyTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void StaticUploadsContainPixelsWithoutExifOrTrailingPrivateData(bool jpeg)
    {
        using var source = new Image<Rgba32>(16, 16, new Rgba32(60, 90, 120));
        source.Metadata.ExifProfile = new ExifProfile();
        source.Metadata.ExifProfile.SetValue(ExifTag.Artist, "PRIVATE-CAMERA-OWNER");
        using var stream = new MemoryStream();
        if (jpeg) source.SaveAsJpeg(stream); else source.SaveAsPng(stream);
        stream.Write(Encoding.UTF8.GetBytes("PRIVATE-TRAILER"));
        var clean = HeadshotImage.Normalize(stream.ToArray(), false);
        using var decoded = Image.Load<Rgba32>(clean.Bytes);
        Assert.That(decoded.Metadata.ExifProfile, Is.Null);
        Assert.That(Encoding.UTF8.GetString(clean.Bytes), Does.Not.Contain("PRIVATE"));
        Assert.That(decoded[8,8].A, Is.EqualTo(255));
        Assert.That(clean.Hash, Has.Length.EqualTo(64));
    }
    [Test]
    public void GifKeepsAnimationTimingButRemovesCommentsAndMetadata()
    {
        using var source = new Image<Rgba32>(8, 8, new Rgba32(255, 0, 0));
        source.Frames.AddFrame(source.Frames.RootFrame);
        source.Frames[0].Metadata.GetGifMetadata().FrameDelay = 7;
        source.Frames[1].Metadata.GetGifMetadata().FrameDelay = 13;
        source.Metadata.GetGifMetadata().RepeatCount = 4;
        source.Metadata.GetGifMetadata().Comments.Add("PRIVATE-GIF-COMMENT");
        using var stream = new MemoryStream(); source.SaveAsGif(stream);
        var clean = HeadshotImage.Normalize(stream.ToArray(), true);
        using var decoded = Image.Load<Rgba32>(clean.Bytes);
        Assert.That(decoded.Frames.Count, Is.EqualTo(2));
        Assert.That(decoded.Frames[0].Metadata.GetGifMetadata().FrameDelay, Is.EqualTo(7));
        Assert.That(decoded.Frames[1].Metadata.GetGifMetadata().FrameDelay, Is.EqualTo(13));
        Assert.That(decoded.Metadata.GetGifMetadata().RepeatCount, Is.EqualTo(4));
        Assert.That(decoded.Metadata.GetGifMetadata().Comments, Is.Empty);
        Assert.That(Encoding.UTF8.GetString(clean.Bytes), Does.Not.Contain("PRIVATE"));
    }
}
