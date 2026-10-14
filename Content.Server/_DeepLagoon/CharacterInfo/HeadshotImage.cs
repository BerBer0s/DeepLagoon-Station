using System.IO;
using Content.Shared._DeepLagoon.CharacterInfo;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Server._DeepLagoon.CharacterInfo;

/// <summary>Only pixels and animation controls leave the upload boundary. No source metadata is copied.</summary>
public static class HeadshotImage
{
    public sealed record Clean(byte[] Bytes, string Mime, string Hash);

    public static Clean Normalize(byte[] bytes, bool extended)
    {
        var mime = HeadshotSystem.ValidateHeader(bytes, extended);
        using var input = Image.Load<Rgba32>(bytes);
        using var output = new Image<Rgba32>(input.Width, input.Height);
        var pixels = new Rgba32[input.Width * input.Height];
        for (var i = 0; i < input.Frames.Count; i++)
        {
            input.Frames[i].CopyPixelDataTo(pixels);
            using var frameImage = Image.LoadPixelData<Rgba32>(pixels, input.Width, input.Height);
            var target = output.Frames.AddFrame(frameImage.Frames.RootFrame);
            if (mime == "image/gif")
            {
                var sourceControls = input.Frames[i].Metadata.GetGifMetadata();
                var controls = target.Metadata.GetGifMetadata();
                controls.FrameDelay = sourceControls.FrameDelay;
                controls.DisposalMethod = sourceControls.DisposalMethod;
            }
        }
        output.Frames.RemoveFrame(0);
        if (mime == "image/gif")
            output.Metadata.GetGifMetadata().RepeatCount = input.Metadata.GetGifMetadata().RepeatCount;
        using var encoded = new MemoryStream();
        IImageEncoder encoder = mime switch
        {
            "image/gif" => new GifEncoder(),
            "image/jpeg" => new JpegEncoder { Quality = 90, SkipMetadata = true },
            _ => new PngEncoder { ColorType = PngColorType.RgbWithAlpha, SkipMetadata = true },
        };
        output.Save(encoded, encoder);
        if (encoded.Length > (extended ? HeadshotLimits.SponsorMaxBytes : HeadshotLimits.MaxBytes))
            throw new InvalidDataException("Sanitized image exceeds the byte budget.");
        var result = encoded.ToArray();
        return new Clean(result, mime, HeadshotLimits.ContentHash(result));
    }
}
