using System.IO;
using System.Text;
using Content.Shared._DeepLagoon.Tattoos;
using Robust.Client.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._DeepLagoon.Tattoos;

/// <summary>Portable visible PNG with a private ancillary chunk preserving the two ink channels.</summary>
public static class TattooPng
{
    public const int MaxFileBytes = 512 * 1024;
    private const int MaxDimension = 2048;

    private static int ReadInt(byte[] bytes, int p) => bytes[p] << 24 | bytes[p + 1] << 16 | bytes[p + 2] << 8 | bytes[p + 3];
    private static void WriteInt(Stream stream, int value)
    {
        for (var shift = 24; shift >= 0; shift -= 8) stream.WriteByte(unchecked((byte)(value >> shift)));
    }
    private static uint Crc(byte[] bytes, int start, int count)
    {
        var crc = uint.MaxValue;
        for (var i = start; i < start + count; i++)
        {
            crc ^= bytes[i];
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
        }
        return ~crc;
    }
    private static bool Type(byte[] b, int p, string name) => b[p + 4] == name[0] && b[p + 5] == name[1]
        && b[p + 6] == name[2] && b[p + 7] == name[3];

    public static byte[] Export(Tattoo tattoo)
    {
        var raw = tattoo.Clone(); raw.X = raw.Y = .5f; raw.Scale = raw.Opacity = 1; raw.Rotation = 0; raw.Mirrored = false;
        using var mask = new Image<Rgba32>(32, 32, new Rgba32(255, 255, 255, 255));
        using var normal = TattooRasterizer.Render(raw, false, mask);
        using var glow = TattooRasterizer.Render(raw, true, mask);
        var normalBytes = tattoo.Strokes.Count == 0 ? Convert.FromBase64String(tattoo.NormalBitmap) : Encode(normal);
        var glowBytes = tattoo.Strokes.Count == 0 ? Convert.FromBase64String(tattoo.GlowBitmap) : Encode(glow);
        var np = normal.GetPixelSpan(); var gp = glow.GetPixelSpan();
        for (var i = 0; i < np.Length; i++)
        {
            var n = np[i]; var g = gp[i]; var ga = g.A / 255f; var na = n.A / 255f;
            var a = ga + na * (1 - ga);
            if (a <= 0) continue;
            np[i] = new Rgba32(unchecked((byte)((g.R * ga + n.R * na * (1 - ga)) / a)),
                unchecked((byte)((g.G * ga + n.G * na * (1 - ga)) / a)),
                unchecked((byte)((g.B * ga + n.B * na * (1 - ga)) / a)), unchecked((byte)(a * 255)));
        }
        var preview = Convert.FromBase64String(TattooSpriteFrames.Png(normal).Split(',')[1]);
        using var chunk = new MemoryStream();
        // d: ancillary, l: private, T: reserved bit clear, G: unsafe to copy after external editing.
        chunk.WriteByte(100); chunk.WriteByte(108); chunk.WriteByte(84); chunk.WriteByte(71); chunk.WriteByte(2);
        WriteInt(chunk, normalBytes.Length); WriteInt(chunk, glowBytes.Length);
        var proof = Encoding.ASCII.GetBytes(tattoo.Strokes.Count == 0 ? tattoo.ArtworkProof : "");
        WriteInt(chunk, proof.Length); chunk.Write(proof);
        chunk.Write(normalBytes); chunk.Write(glowBytes);
        var chunkBytes = chunk.ToArray();
        using var result = new MemoryStream();
        result.Write(preview, 0, preview.Length - 12); // Insert immediately before IEND.
        WriteInt(result, chunkBytes.Length - 4); result.Write(chunkBytes);
        WriteInt(result, unchecked((int)Crc(chunkBytes, 0, chunkBytes.Length)));
        result.Write(preview, preview.Length - 12, 12);
        if (result.Length > MaxFileBytes) throw new InvalidDataException("Изображение превышает 512 КБ.");
        return result.ToArray();
    }

    private static byte[] Encode(Image<Rgba32> image)
    {
        foreach (var pixel in image.GetPixelSpan())
            if (pixel.A != 0) return Convert.FromBase64String(TattooSpriteFrames.Png(image).Split(',')[1]);
        return [];
    }

    public static Tattoo Import(byte[] bytes, Tattoo template, bool allowExternalPng = false)
    {
        if (bytes.Length < 45 || bytes.Length > MaxFileBytes || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78
            || bytes[3] != 71 || bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10
            || ReadInt(bytes, 8) != 13 || !Type(bytes, 8, "IHDR"))
            throw new InvalidDataException("Выберите PNG размером до 512 КБ.");
        var width = ReadInt(bytes, 16); var height = ReadInt(bytes, 20);
        if (width is < 1 or > MaxDimension || height is < 1 or > MaxDimension)
            throw new InvalidDataException("Максимальный размер PNG — 2048×2048.");
        var normal = ""; var glow = ""; var proof = ""; var metadata = false; var end = false; var data = false;
        using var clean = new MemoryStream(); clean.Write(bytes, 0, 8);
        for (var p = 8; p <= bytes.Length - 12;)
        {
            var length = ReadInt(bytes, p);
            if (length < 0 || length > bytes.Length - p - 12 || unchecked((uint)ReadInt(bytes, p + 8 + length)) != Crc(bytes, p + 4, length + 4))
                throw new InvalidDataException("PNG повреждён.");
            if (Type(bytes, p, "acTL") || Type(bytes, p, "fcTL") || Type(bytes, p, "fdAT"))
                throw new InvalidDataException("Для татуировки нужен неподвижный PNG.");
            if (Type(bytes, p, "IDAT")) data = true;
            // Discard compressed profiles/text and unrelated metadata before invoking the decoder.
            if (Type(bytes, p, "IHDR") || Type(bytes, p, "PLTE") || Type(bytes, p, "tRNS")
                || Type(bytes, p, "IDAT") || Type(bytes, p, "IEND")) clean.Write(bytes, p, length + 12);
            if (Type(bytes, p, "dlTG"))
            {
                if (metadata || length < 9 || bytes[p + 8] is not (1 or 2)) throw new InvalidDataException("Неизвестный формат татуировки.");
                metadata = true;
                var n = ReadInt(bytes, p + 9); var g = ReadInt(bytes, p + 13);
                var header = 9;
                if (bytes[p + 8] == 2)
                {
                    if (length < 13) throw new InvalidDataException("Повреждена подпись.");
                    var proofLength = ReadInt(bytes, p + 17);
                    if (proofLength < 0 || proofLength > 128 || proofLength > length - 13) throw new InvalidDataException("Повреждена подпись.");
                    proof = Encoding.ASCII.GetString(bytes, p + 21, proofLength); header = 13 + proofLength;
                }
                if (n < 0 || g < 0 || n > length - header || g != length - header - n) throw new InvalidDataException("Повреждён канал чернил.");
                normal = Convert.ToBase64String(bytes, p + 8 + header, n); glow = Convert.ToBase64String(bytes, p + 8 + header + n, g);
            }
            if (Type(bytes, p, "IEND")) { end = data && length == 0 && p + 12 == bytes.Length; break; }
            p += length + 12;
        }
        if (!end) throw new InvalidDataException("PNG не завершён.");
        var result = template.Clone(); result.Strokes.Clear();
        if (!metadata)
        {
            if (!allowExternalPng) throw new InvalidDataException("Импорт стороннего PNG доступен только с третьим тиром доната.");
            clean.Position = 0; using var image = Image.Load<Rgba32>(clean);
            var scale = Math.Min(Tattoo.Resolution / (float)width, Tattoo.Resolution / (float)height);
            var w = Math.Max(1, unchecked((int)MathF.Round(width * scale)));
            var h = Math.Max(1, unchecked((int)MathF.Round(height * scale)));
            using var resized = image.Clone(c => c.Resize(w, h));
            using var padded = new Image<Rgba32>(Tattoo.Resolution, Tattoo.Resolution);
            var src = resized.GetPixelSpan(); var dst = padded.GetPixelSpan();
            var left = (Tattoo.Resolution - w) / 2; var top = (Tattoo.Resolution - h) / 2;
            for (var y = 0; y < h; y++) src.Slice(y * w, w).CopyTo(dst.Slice((top + y) * Tattoo.Resolution + left, w));
            normal = TattooSpriteFrames.Png(padded).Split(',')[1];
        }
        if (metadata && proof.Length == 0 && !allowExternalPng)
            throw new InvalidDataException("Этот старый экспорт не подписан сервером. Откройте сохранённый рисунок в профиле и экспортируйте его заново.");
        result.NormalBitmap = normal; result.GlowBitmap = glow; result.ArtworkProof = proof;
        if (!result.IsValid()) throw new InvalidDataException("Изображение превышает допустимый объём татуировки.");
        foreach (var png in new[] { normal, glow })
            if (png.Length > 0) { using var input = new MemoryStream(Convert.FromBase64String(png)); using var image = Image.Load<Rgba32>(input); }
        return result;
    }
}
