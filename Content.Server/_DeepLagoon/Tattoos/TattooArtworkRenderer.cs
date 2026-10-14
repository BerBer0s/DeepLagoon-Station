using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Content.Shared._DeepLagoon.Tattoos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Server._DeepLagoon.Tattoos;

/// <summary>Only brush operations and a previously authorized base enter the rasterizer.</summary>
public static class TattooArtworkRenderer
{
    public sealed class Layer
    {
        public bool UseBase { get; set; }
        public bool Visible { get; set; } = true;
        public float Opacity { get; set; } = 1;
        public List<Stroke> Strokes { get; set; } = new();
    }
    public sealed class Stroke
    {
        public string Color { get; set; } = "";
        public float Width { get; set; }
        public bool Glow { get; set; }
        public bool Erase { get; set; }
        public List<float[]> Points { get; set; } = new();
    }
    private const int Size = Tattoo.Resolution;
    public static Tattoo Render(Tattoo basis, string recipe)
    {
        if (recipe.Length > 240000) throw new InvalidDataException("Слишком много штрихов. Завершайте рисунок чаще.");
        var layers = JsonSerializer.Deserialize<List<Layer>>(recipe, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 8 });
        if (layers == null || layers.Count is < 1 or > Tattoo.MaxLayers || layers.Any(l => l == null || l.Strokes == null)
            || layers.Count(l => l.UseBase) > 1 || layers.Sum(l => l.Strokes.Count) > 1024) throw new InvalidDataException("Некорректные слои.");
        var points = 0;
        foreach (var layer in layers)
        {
            if (!float.IsFinite(layer.Opacity) || layer.Opacity is < 0 or > 1) throw new InvalidDataException("Некорректная прозрачность.");
            foreach (var stroke in layer.Strokes)
            {
                if (stroke == null || stroke.Color == null || stroke.Color.Length != 9 || stroke.Color[0] != '#'
                    || !stroke.Color.Skip(1).All(Uri.IsHexDigit) || !float.IsFinite(stroke.Width) || stroke.Width is < .002f or > .2f
                    || stroke.Points == null || stroke.Points.Count is < 1 or > 2048
                    || stroke.Points.Any(p => p == null || p.Length != 2 || !float.IsFinite(p[0]) || !float.IsFinite(p[1])
                        || p[0] is < 0 or > 1 || p[1] is < 0 or > 1)) throw new InvalidDataException("Некорректный штрих.");
                points += stroke.Points.Count;
            }
        }
        if (points > 16384) throw new InvalidDataException("Слишком много точек. Завершайте рисунок чаще.");
        var result = new[] { new Rgba32[Size * Size], new Rgba32[Size * Size] };
        long work = 0;
        foreach (var layer in layers)
        {
            if (!layer.Visible) continue;
            var ink = layer.UseBase ? new[] { Decode(basis.NormalBitmap), Decode(basis.GlowBitmap) }
                : new[] { new Rgba32[Size * Size], new Rgba32[Size * Size] };
            if (layer.UseBase)
                foreach (var old in basis.Strokes)
                    Paint(ink, new Stroke { Color = old.Color, Width = old.Width, Glow = old.Glow, Erase = old.Erase,
                        Points = old.Points.Select(p => new[] { p.X, p.Y }).ToList() }, ref work);
            foreach (var stroke in layer.Strokes) Paint(ink, stroke, ref work);
            for (var c = 0; c < 2; c++) for (var i = 0; i < result[c].Length; i++)
            {
                result[c][i].A = (byte) (result[c][i].A * (1 - ink[1 - c][i].A / 255f * layer.Opacity));
                result[c][i] = Over(result[c][i], ink[c][i], layer.Opacity);
            }
        }
        var tattoo = basis.Clone(); tattoo.Strokes.Clear();
        tattoo.NormalBitmap = Encode(result[0]); tattoo.GlowBitmap = Encode(result[1]); tattoo.ArtworkProof = "";
        if (!tattoo.IsValid()) throw new InvalidDataException("Рисунок превышает допустимый объём.");
        return tattoo;
    }
    private static Rgba32[] Decode(string png)
    {
        var pixels = new Rgba32[Size * Size];
        if (png.Length == 0) return pixels;
        using var image = Image.Load<Rgba32>(Convert.FromBase64String(png));
        if (image.Width != Size || image.Height != Size || image.Frames.Count != 1) throw new InvalidDataException("Некорректное изображение.");
        image.CopyPixelDataTo(pixels); return pixels;
    }
    private static string Encode(Rgba32[] pixels)
    {
        if (pixels.All(p => p.A == 0)) return "";
        using var image = Image.LoadPixelData<Rgba32>(pixels, Size, Size);
        using var stream = new MemoryStream(); image.Save(stream, new PngEncoder { ColorType = PngColorType.RgbWithAlpha, SkipMetadata = true });
        return Convert.ToBase64String(stream.ToArray());
    }
    private static Rgba32 Over(Rgba32 old, Rgba32 color, float opacity)
    {
        var alpha = color.A / 255f * opacity; var oa = old.A / 255f; var combined = alpha + oa * (1 - alpha);
        if (combined <= 0) return default;
        return new Rgba32((byte)((color.R * alpha + old.R * oa * (1 - alpha)) / combined),
            (byte)((color.G * alpha + old.G * oa * (1 - alpha)) / combined),
            (byte)((color.B * alpha + old.B * oa * (1 - alpha)) / combined), (byte)(combined * 255));
    }
    private static void Paint(Rgba32[][] channels, Stroke stroke, ref long work)
    {
        var color = Convert.FromHexString(stroke.Color[1..]); var ink = new Rgba32(color[0], color[1], color[2], color[3]);
        var radius = Math.Max(.5f, stroke.Width * Size / 2);
        for (var index = 0; index < Math.Max(1, stroke.Points.Count - 1); index++)
        {
            var a = stroke.Points[index]; var b = stroke.Points[Math.Min(index + 1, stroke.Points.Count - 1)];
            var start = new Vector2(a[0], a[1]) * Size; var end = new Vector2(b[0], b[1]) * Size;
            var left = Math.Clamp((int)MathF.Floor(Math.Min(start.X, end.X) - radius - 1), 0, Size);
            var right = Math.Clamp((int)MathF.Ceiling(Math.Max(start.X, end.X) + radius + 1), 0, Size);
            var top = Math.Clamp((int)MathF.Floor(Math.Min(start.Y, end.Y) - radius - 1), 0, Size);
            var bottom = Math.Clamp((int)MathF.Ceiling(Math.Max(start.Y, end.Y) + radius + 1), 0, Size);
            work += (right - left) * (bottom - top);
            if (work > 8000000) throw new InvalidDataException("Рисунок слишком сложный. Завершайте его чаще.");
            var delta = end - start; var length = delta.LengthSquared();
            for (var y = top; y < bottom; y++) for (var x = left; x < right; x++)
            {
                var point = new Vector2(x + .5f, y + .5f);
                var t = length < .00001f ? 0 : Math.Clamp(Vector2.Dot(point - start, delta) / length, 0, 1);
                var alpha = Math.Clamp(radius + .5f - Vector2.Distance(point, start + delta * t), 0, 1);
                for (var c = 0; c < 2; c++)
                    if (stroke.Erase || stroke.Glow != (c == 1)) channels[c][y * Size + x].A = (byte)(channels[c][y * Size + x].A * (1 - alpha * ink.A / 255f));
                    else channels[c][y * Size + x] = Over(channels[c][y * Size + x], ink, alpha);
            }
        }
    }
}
