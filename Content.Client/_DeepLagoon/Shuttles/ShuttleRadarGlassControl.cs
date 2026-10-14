using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client._DeepLagoon.Shuttles;

/// <summary>
/// Circular aperture and glass above the native radar, including its child controls.
/// One analytic shader pass, with no framebuffer copies or off-screen render targets.
/// </summary>
public sealed class ShuttleRadarGlassControl : Control
{
    private readonly ShaderInstance _glass;

    public ShuttleRadarGlassControl()
    {
        MouseFilter = MouseFilterMode.Stop;
        RectClipContent = true;
        _glass = IoCManager.Resolve<IPrototypeManager>()
            .Index<ShaderPrototype>("ShuttleRadarGlass").InstanceUnique();
    }

    // Pass input through the glass; intercept the hidden corners, including dock buttons.
    protected override bool HasPoint(Vector2 point)
    {
        var radius = MathF.Max(0, MathF.Min(Size.X, Size.Y) / 2 - 4);
        return base.HasPoint(point) && Vector2.DistanceSquared(point, Size / 2) >= radius * radius;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (PixelSize.X <= 0 || PixelSize.Y <= 0)
            return;

        _glass.SetParameter("viewport_size", (Vector2)PixelSize);
        _glass.SetParameter("rim_width", 4 * UIScale);
        handle.UseShader(_glass);
        handle.DrawTextureRect(Texture.White, PixelSizeBox);
        handle.UseShader(null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _glass.Dispose();
        base.Dispose(disposing);
    }
}
