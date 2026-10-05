using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>An inline HSV wheel with brightness and opacity controls.</summary>
public sealed class ChatColorPicker : BoxContainer
{
    public event Action<Color>? OnColorChanged;
    public ChatColorWheel Wheel { get; }
    public Slider Brightness { get; }
    public Slider Opacity { get; }

    public ChatColorPicker(Color color)
    {
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 4;
        HorizontalExpand = true;
        var preview = new PanelContainer { SetHeight = 16, HorizontalExpand = true };
        var toggle = new Button { Text = Loc.GetString("chat-panel-pick-color"), ToggleMode = true };
        var editor = new BoxContainer { Orientation = LayoutOrientation.Vertical, Visible = false };
        Wheel = new ChatColorWheel { SelectedColor = color, HorizontalExpand = true };
        var hsv = Color.ToHsv(color);
        Brightness = new Slider { MinValue = 0, MaxValue = 1, Value = hsv.Z, HorizontalExpand = true };
        Opacity = new Slider { MinValue = 0, MaxValue = 1, Value = color.A, HorizontalExpand = true };
        preview.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        toggle.OnToggled += args => editor.Visible = args.Pressed;
        Wheel.OnColorChanged += selected =>
        {
            preview.PanelOverride = new StyleBoxFlat { BackgroundColor = selected };
            OnColorChanged?.Invoke(selected);
        };
        Brightness.OnValueChanged += _ => Wheel.SetBrightness(Brightness.Value);
        Opacity.OnValueChanged += _ => Wheel.SetOpacity(Opacity.Value);
        AddChild(preview);
        AddChild(toggle);
        editor.AddChild(Wheel);
        editor.AddChild(new Label { Text = Loc.GetString("chat-panel-brightness") });
        editor.AddChild(Brightness);
        editor.AddChild(new Label { Text = Loc.GetString("chat-panel-opacity") });
        editor.AddChild(Opacity);
        AddChild(editor);
    }
}

public sealed class ChatColorWheel : Control
{
    public event Action<Color>? OnColorChanged;
    private Vector4 _hsv = new(0, 0, 1, 1);
    private bool _dragging;
    private DrawVertexUV2DColor[]? _vertices;
    public Color SelectedColor
    {
        get => Color.FromHsv(_hsv);
        set { _hsv = Color.ToHsv(value); _vertices = null; }
    }

    public ChatColorWheel()
    {
        SetHeight = 150;
        MouseFilter = MouseFilterMode.Stop;
    }

    public void SetBrightness(float value) { _hsv.Z = Math.Clamp(value, 0, 1); Notify(); }
    public void SetOpacity(float value) { _hsv.W = Math.Clamp(value, 0, 1); Notify(); }

    public void SelectAt(Vector2 position)
    {
        var delta = position - Size / 2;
        var radius = MathF.Max(1, MathF.Min(Width, Height) / 2 - 4);
        _hsv.X = (MathF.Atan2(delta.Y, delta.X) / MathF.Tau + 1) % 1;
        _hsv.Y = Math.Clamp(delta.Length() / radius, 0, 1);
        Notify();
    }

    private void Notify()
    {
        _vertices = null;
        OnColorChanged?.Invoke(SelectedColor);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick) return;
        _dragging = true;
        SelectAt(args.RelativePosition);
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function == EngineKeyFunctions.UIClick) { _dragging = false; args.Handle(); }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging) SelectAt(args.RelativePosition);
    }

    protected override void Resized() { base.Resized(); _vertices = null; }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var center = PixelSize / 2;
        var radius = MathF.Max(1, MathF.Min(PixelWidth, PixelHeight) / 2 - 4 * UIScale);
        if (_vertices == null)
        {
            const int segments = 96;
            const int rings = 8;
            _vertices = new DrawVertexUV2DColor[segments * rings * 6];
            var index = 0;
            DrawVertexUV2DColor Vertex(float hue, float saturation)
            {
                var angle = hue * MathF.Tau;
                var pos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * saturation * radius;
                var color = Color.FromHsv(new Vector4(hue % 1, saturation, _hsv.Z, 1));
                return new DrawVertexUV2DColor(pos, Color.FromSrgb(color * handle.Modulate));
            }
            for (var ring = 0; ring < rings; ring++)
            for (var segment = 0; segment < segments; segment++)
            {
                var h0 = segment / (float) segments;
                var h1 = (segment + 1) / (float) segments;
                var s0 = ring / (float) rings;
                var s1 = (ring + 1) / (float) rings;
                var a = Vertex(h0, s0); var b = Vertex(h1, s0);
                var c = Vertex(h1, s1); var d = Vertex(h0, s1);
                _vertices[index++] = a; _vertices[index++] = b; _vertices[index++] = c;
                _vertices[index++] = a; _vertices[index++] = c; _vertices[index++] = d;
            }
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _vertices);
        var angleSelected = _hsv.X * MathF.Tau;
        var marker = center + new Vector2(MathF.Cos(angleSelected), MathF.Sin(angleSelected)) * _hsv.Y * radius;
        handle.DrawCircle(marker, 5 * UIScale, Color.Black, false);
        handle.DrawCircle(marker, 4 * UIScale, Color.White, false);
    }
}
