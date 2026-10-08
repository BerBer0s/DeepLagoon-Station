using System;
using System.IO;
using System.Linq;
using Robust.Shared.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Robust.Client.ResourceManagement;

/// <summary>Content-owned adapter for sandboxed UI sprite previews.</summary>
public static class DeepLagoonPackedSpriteFrames
{
    public static string ReadFrame(Image<Rgba32> image, string stateName)
    {
        var text = image.Metadata.GetPngMetadata().TextData
            .First(data => data.Keyword == RsiLoading.RsicPngField).Value;
        var metadata = RsiLoading.LoadRsiMetadata(text);
        var counts = RsiLoading.CalculateFrameCounts(metadata);
        var size = metadata.Size;
        var columns = image.Width / size.X;
        var offset = 0;
        for (var i = 0; i < metadata.States.Length; i++)
        {
            if (metadata.States[i].StateId == stateName)
            {
                // GPU AtlasTexture coordinates include a meta-atlas offset.
                // RSIC bytes contain only this RSI's original packed sheet.
                var region = new Rectangle(offset % columns * size.X, offset / columns * size.Y, size.X, size.Y);
                using var frame = image.Clone(context => context.Crop(region));
                using var output = new MemoryStream();
                frame.SaveAsPng(output);
                return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
            }
            offset += counts[i];
        }
        throw new InvalidDataException("RSIC state not found: " + stateName);
    }
}
