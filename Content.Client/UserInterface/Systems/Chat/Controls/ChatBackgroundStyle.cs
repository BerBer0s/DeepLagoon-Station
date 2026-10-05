using Robust.Client.Graphics;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatBackgroundStyle : StyleBox
{
    public Color Top;
    public Color Bottom;
    public Texture? Image;
    public bool Gradient;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        if (Gradient)
        {
            const int steps = 64;
            for (var i = 0; i < steps; i++)
            {
                var t = i / (float) (steps - 1);
                var color = new Color(Top.R + (Bottom.R - Top.R) * t, Top.G + (Bottom.G - Top.G) * t,
                    Top.B + (Bottom.B - Top.B) * t, Top.A + (Bottom.A - Top.A) * t);
                handle.DrawRect(new UIBox2(box.Left, box.Top + box.Height * i / steps, box.Right, box.Top + box.Height * (i + 1) / steps), color);
            }
        }
        else handle.DrawRect(box, Top);
        if (Image != null) handle.DrawTextureRect(Image, box, new Color(1f, 1f, 1f, 0.4f));
    }
}
