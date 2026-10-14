using System.Linq;
using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.Tattoos;

/// <summary>Baked artwork with editable placement. Strokes are retained only for legacy profiles.</summary>
[Serializable, NetSerializable, DataDefinition]
public sealed partial class Tattoo
{
    public const int MaxTattoos = 8;
    public const int MaxStrokes = 64;
    public const int MaxPoints = 512;
    public const int MaxStrokePoints = 128;
    public const int Resolution = 256;
    public const int MaxLayers = 4;
    public const int MaxBitmapChars = 131072;
    public const int MaxTattooBitmapChars = 262144;
    public const int MaxBakePayload = 300000;
    public static readonly HumanoidVisualLayers[] BodyParts =
    [HumanoidVisualLayers.Chest, HumanoidVisualLayers.Head, HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RArm, HumanoidVisualLayers.LLeg, HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LHand, HumanoidVisualLayers.RHand, HumanoidVisualLayers.LFoot, HumanoidVisualLayers.RFoot];

    [DataField] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [DataField] public HumanoidVisualLayers BodyPart { get; set; } = HumanoidVisualLayers.Chest;
    // RSI order: south, north, east, west. Each view is intentionally independent.
    [DataField] public int Direction { get; set; }
    [DataField] public float X { get; set; } = .5f;
    [DataField] public float Y { get; set; } = .5f;
    [DataField] public float Scale { get; set; } = 1;
    [DataField] public float Rotation { get; set; }
    [DataField] public float Opacity { get; set; } = 1;
    [DataField] public List<TattooStroke> Strokes { get; set; } = new();
    [DataField] public string NormalBitmap { get; set; } = "";
    [DataField] public string GlowBitmap { get; set; } = "";
    [DataField] public bool Mirrored { get; set; }
    /// <summary>Server-issued, account-bound proof for the two ink bitmaps. Never a client signing key.</summary>
    [DataField] public string ArtworkProof { get; set; } = "";

    public bool IsValid() => Guid.TryParseExact(Id, "N", out _) && BodyParts.Contains(BodyPart)
        && Direction is >= 0 and < 4 && float.IsFinite(X) && X is >= -1 and <= 2
        && float.IsFinite(Y) && Y is >= -1 and <= 2 && float.IsFinite(Scale) && Scale is >= .05f and <= 4
        && float.IsFinite(Rotation) && Rotation is >= -180 and <= 180
        && float.IsFinite(Opacity) && Opacity is >= 0 and <= 1 && Strokes != null
        && Strokes.Count <= MaxStrokes && Strokes.All(s => s != null && s.IsValid())
        && Strokes.Sum(s => s.Points.Count) <= MaxPoints
        && TattooBitmap.ValidPng(NormalBitmap) && TattooBitmap.ValidPng(GlowBitmap)
        && NormalBitmap.Length + GlowBitmap.Length <= MaxTattooBitmapChars
        && ArtworkProof != null && ArtworkProof.Length <= 128;

    public Tattoo Clone() => new()
    {
        Id = Id, BodyPart = BodyPart, Direction = Direction, X = X, Y = Y,
        Scale = Scale, Rotation = Rotation, Opacity = Opacity,
        Strokes = Strokes.Select(s => s.Clone()).ToList(),
        NormalBitmap = NormalBitmap, GlowBitmap = GlowBitmap, Mirrored = Mirrored, ArtworkProof = ArtworkProof,
    };

    public static List<Tattoo> Sanitize(IEnumerable<Tattoo>? tattoos) => tattoos?.Take(MaxTattoos)
        .Where(t => t != null && t.IsValid()).DistinctBy(t => t.Id).Select(t => t.Clone()).ToList() ?? new();

    public bool ContentEquals(Tattoo other) => Id == other.Id && BodyPart == other.BodyPart
        && Direction == other.Direction && X == other.X && Y == other.Y && Scale == other.Scale
        && Rotation == other.Rotation && Opacity == other.Opacity && Strokes.Count == other.Strokes.Count
        && Strokes.Zip(other.Strokes).All(pair => pair.First.ContentEquals(pair.Second))
        && Mirrored == other.Mirrored && NormalBitmap == other.NormalBitmap && GlowBitmap == other.GlowBitmap && ArtworkProof == other.ArtworkProof;
}

public static class TattooBitmap
{

    // Reject oversized dimensions/animation before any decoder touches client-provided PNGs.
    public static bool ValidPng(string? value)
    {
        if (value == "") return true;
        if (value == null || value.Length > Tattoo.MaxBitmapChars) return false;
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value); } catch (Exception) { return false; }
        if (bytes.Length < 45 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71
            || bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10) return false;
        int Number(int p) => bytes[p] << 24 | bytes[p + 1] << 16 | bytes[p + 2] << 8 | bytes[p + 3];
        bool Chunk(int p, string name) => bytes[p + 4] == name[0] && bytes[p + 5] == name[1]
            && bytes[p + 6] == name[2] && bytes[p + 7] == name[3];
        if (Number(8) != 13 || !Chunk(8, "IHDR") || Number(16) != Tattoo.Resolution || Number(20) != Tattoo.Resolution
            || bytes[24] != 8 || bytes[25] != 6) return false;
        var data = false;
        for (var p = 33; p <= bytes.Length - 12;)
        {
            var length = Number(p);
            if (length < 0 || length > bytes.Length - p - 12) return false;
            // Canvas emits RGBA PNG with optional sRGB, but never APNG or metadata payloads.
            if (Chunk(p, "IDAT")) data = true;
            else if (Chunk(p, "IEND")) return data && length == 0 && p + 12 == bytes.Length;
            else if (!Chunk(p, "sRGB") && !Chunk(p, "gAMA") && !Chunk(p, "cHRM") && !Chunk(p, "pHYs")) return false;
            p += length + 12;
        }
        return false;
    }
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class TattooStroke
{
    [DataField] public string Color { get; set; } = "#182030FF";
    [DataField] public float Width { get; set; } = .015f;
    [DataField] public bool Glow { get; set; }
    [DataField] public bool Erase { get; set; }
    [DataField] public List<TattooPoint> Points { get; set; } = new();

    public bool IsValid() => Color != null && Color.Length == 9 && Color[0] == '#'
        && Color.Skip(1).All(Uri.IsHexDigit) && float.IsFinite(Width) && Width is >= .002f and <= .2f
        && Points != null && Points.Count is > 0 and <= Tattoo.MaxStrokePoints && Points.All(p => p.IsValid());
    public TattooStroke Clone() => new() { Color = Color, Width = Width, Glow = Glow, Erase = Erase, Points = new(Points) };
    public bool ContentEquals(TattooStroke other) => Color == other.Color && Width == other.Width
        && Glow == other.Glow && Erase == other.Erase && Points.SequenceEqual(other.Points);
}

[Serializable, NetSerializable, DataDefinition]
public partial record struct TattooPoint
{
    [DataField] public float X { get; set; }
    [DataField] public float Y { get; set; }
    public TattooPoint(float x, float y) { X = x; Y = y; }
    public bool IsValid() => float.IsFinite(X) && float.IsFinite(Y) && X is >= 0 and <= 1 && Y is >= 0 and <= 1;
}
