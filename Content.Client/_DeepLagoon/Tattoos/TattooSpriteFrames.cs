using System.IO;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._DeepLagoon.Tattoos;

public static class TattooSpriteFrames
{
    /// <summary>CPU frames from both loose RSI PNGs and shipped RSIC sheets, including animation.</summary>
    public static Image<Rgba32> Read(RSI.State state, int direction, int frame = 0)
    {
        var resources = IoCManager.Resolve<IResourceCache>();
        var root = state.RSI.Path;
        var packed = !resources.TryContentFileRead(root / (state.StateId.Name + ".png"), out var stream);
        stream ??= resources.ContentFileRead(root.WithExtension("rsic"));
        using var input = stream;
        using var image = Image.Load<Rgba32>(input);
        Rectangle region;
        if (packed)
        {
            var regions = state.RSI.Select(s => ((AtlasTexture) s.Frame0).SubRegion).ToArray();
            var selected = ((AtlasTexture) state.GetFrame(unchecked((RsiDirection) direction), frame)).SubRegion;
            region = new Rectangle((int) (selected.Left - regions.Min(r => r.Left)),
                (int) (selected.Top - regions.Min(r => r.Top)), state.Size.X, state.Size.Y);
        }
        else
        {
            // GPU frame offsets retain the original PNG index even after differing directional
            // animation delays have been folded. DelayCount is NOT the source PNG frame count.
            var all = state.RSI.SelectMany(s => s.Icons).SelectMany(d => d).Cast<AtlasTexture>().Select(t => t.SubRegion).ToArray();
            var originX = all.Min(r => r.Left);
            var originY = all.Min(r => r.Top);
            var atlasColumns = (int) (all.Max(r => r.Right) - originX) / state.Size.X;
            int Index(Robust.Shared.Maths.UIBox2 box) => (int) (box.Top - originY) / state.Size.Y * atlasColumns
                + (int) (box.Left - originX) / state.Size.X;
            var index = Index(((AtlasTexture) state.GetFrame(unchecked((RsiDirection) direction), frame)).SubRegion)
                - Index(((AtlasTexture) state.Frame0).SubRegion);
            var columns = image.Width / state.Size.X;
            region = new Rectangle(index % columns * state.Size.X, index / columns * state.Size.Y, state.Size.X, state.Size.Y);
        }
        return image.Clone(context => context.Crop(region));
    }

    public static string Png(Image<Rgba32> image)
    {
        using var output = new MemoryStream();
        image.SaveAsPng(output);
        return "data:image/png;base64," + Convert.ToBase64String(output.ToArray());
    }
}
