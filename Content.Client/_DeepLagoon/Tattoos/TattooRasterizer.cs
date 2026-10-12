using System.Numerics;
using System.IO;
using Content.Shared._DeepLagoon.Tattoos;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._DeepLagoon.Tattoos;

/// <summary>High-resolution antialiased ink. Masking happens after placement, in body coordinates.</summary>
public static class TattooRasterizer
{
    public static Image<Rgba32> Render(Tattoo tattoo, bool glow, Image<Rgba32> mask)
    {
        var width = Tattoo.Resolution;
        var height = Math.Max(1, mask.Height * width / mask.Width);
        var result = new Image<Rgba32>(width, height);
        var encoded = glow ? tattoo.GlowBitmap : tattoo.NormalBitmap;
        if (encoded.Length > 0)
        {
            using var input = new MemoryStream(Convert.FromBase64String(encoded));
            using var bitmap = Image.Load<Rgba32>(input);
            var source = bitmap.GetPixelSpan();
            var destination = result.GetPixelSpan();
            var angle = -tattoo.Rotation * MathF.PI / 180;
            var sin = MathF.Sin(angle); var cos = MathF.Cos(angle);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var dx = (x + .5f) / width - tattoo.X;
                var dy = (y + .5f) / height - tattoo.Y;
                var sx = ((dx * cos - dy * sin) / tattoo.Scale + .5f) * bitmap.Width - .5f;
                var sy = ((dx * sin + dy * cos) / tattoo.Scale + .5f) * bitmap.Height - .5f;
                if (tattoo.Mirrored) sx = bitmap.Width - 1 - sx;
                var x0 = unchecked((int)MathF.Floor(sx)); var y0 = unchecked((int)MathF.Floor(sy));
                // Bilinear premultiplied sampling keeps scaled transparent edges smooth.
                var sum = Vector4.Zero;
                for (var oy = 0; oy < 2; oy++) for (var ox = 0; ox < 2; ox++)
                {
                    var px = x0 + ox; var py = y0 + oy;
                    if (px < 0 || py < 0 || px >= bitmap.Width || py >= bitmap.Height) continue;
                    var pixel = source[py * bitmap.Width + px];
                    var weight = (ox == 0 ? 1 - (sx - x0) : sx - x0) * (oy == 0 ? 1 - (sy - y0) : sy - y0);
                    var a = pixel.A / 255f * weight;
                    sum += new Vector4(pixel.R / 255f * a, pixel.G / 255f * a, pixel.B / 255f * a, a);
                }
                var old = destination[y * width + x]; var oldA = old.A / 255f;
                var combined = sum.W + oldA * (1 - sum.W);
                if (combined <= 0) { destination[y * width + x] = default; continue; }
                destination[y * width + x] = new Rgba32(
                    unchecked((byte)((sum.X + old.R / 255f * oldA * (1 - sum.W)) / combined * 255)),
                    unchecked((byte)((sum.Y + old.G / 255f * oldA * (1 - sum.W)) / combined * 255)),
                    unchecked((byte)((sum.Z + old.B / 255f * oldA * (1 - sum.W)) / combined * 255)),
                    unchecked((byte)(combined * 255)));
            }
        }
        foreach (var stroke in tattoo.Strokes)
        {
            var color = Color.FromHex(stroke.Color);
            var radius = Math.Max(.5f, stroke.Width * width * tattoo.Scale / 2);
            var erase = stroke.Erase || stroke.Glow != glow;
            var previous = Place(stroke.Points[0], tattoo, width, height);
            if (stroke.Points.Count == 1) Segment(result, previous, previous, radius, color, erase);
            for (var i = 1; i < stroke.Points.Count; i++)
            {
                var next = Place(stroke.Points[i], tattoo, width, height);
                Segment(result, previous, next, radius, color, erase);
                previous = next;
            }
        }
        var pixels = result.GetPixelSpan();
        var maskPixels = mask.GetPixelSpan();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = pixels[y * width + x];
            pixel.A = unchecked((byte) (pixel.A * (maskPixels[y * mask.Height / height * mask.Width + x * mask.Width / width].A / 255f) * tattoo.Opacity));
            pixels[y * width + x] = pixel;
        }
        return result;
    }

    private static Vector2 Place(TattooPoint p, Tattoo tattoo, int width, int height)
    {
        var offset = new Vector2(p.X - .5f, p.Y - .5f) * tattoo.Scale;
        var angle = tattoo.Rotation * MathF.PI / 180;
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new Vector2((offset.X * cos - offset.Y * sin + tattoo.X) * width,
            (offset.X * sin + offset.Y * cos + tattoo.Y) * height);
    }

    private static void Segment(Image<Rgba32> image, Vector2 start, Vector2 end, float radius, Color color, bool erase)
    {
        var left = Math.Clamp(unchecked((int) MathF.Floor(MathF.Min(start.X, end.X) - radius - 1)), 0, image.Width);
        var right = Math.Clamp(unchecked((int) MathF.Ceiling(MathF.Max(start.X, end.X) + radius + 1)), 0, image.Width);
        var top = Math.Clamp(unchecked((int) MathF.Floor(MathF.Min(start.Y, end.Y) - radius - 1)), 0, image.Height);
        var bottom = Math.Clamp(unchecked((int) MathF.Ceiling(MathF.Max(start.Y, end.Y) + radius + 1)), 0, image.Height);
        var delta = end - start;
        var length = delta.LengthSquared();
        var pixels = image.GetPixelSpan();
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var point = new Vector2(x + .5f, y + .5f);
            var t = length < .00001f ? 0 : Math.Clamp(Vector2.Dot(point - start, delta) / length, 0, 1);
            var alpha = Math.Clamp(radius + .5f - Vector2.Distance(point, start + delta * t), 0, 1) * color.A;
            if (alpha <= 0) continue;
            var old = pixels[y * image.Width + x];
            if (erase)
            {
                old.A = unchecked((byte) (old.A * (1 - alpha)));
                pixels[y * image.Width + x] = old;
                continue;
            }
            var oldAlpha = old.A / 255f;
            var combined = alpha + oldAlpha * (1 - alpha);
            pixels[y * image.Width + x] = new Rgba32(
                unchecked((byte) ((color.RByte * alpha + old.R * oldAlpha * (1 - alpha)) / combined)),
                unchecked((byte) ((color.GByte * alpha + old.G * oldAlpha * (1 - alpha)) / combined)),
                unchecked((byte) ((color.BByte * alpha + old.B * oldAlpha * (1 - alpha)) / combined)),
                unchecked((byte) (combined * 255)));
        }
    }
}
