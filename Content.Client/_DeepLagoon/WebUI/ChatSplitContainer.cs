using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Native draggable separator with a visible grip; works beside a CEF control.</summary>
public sealed class ChatSplitContainer : SplitContainer
{
    public ChatSplitContainer()
    {
        SplitWidth = MinDraggableWidth = 12;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        handle.DrawRect(new UIBox2(SplitCenter - 6, 0, SplitCenter + 6, Height), Color.FromHex("#353746"));
        handle.DrawRect(new UIBox2(SplitCenter - 1, 8, SplitCenter + 1, Height - 8), Color.FromHex("#62677F"));
        for (var i = -2; i <= 2; i++)
            handle.DrawCircle(new Vector2(SplitCenter, Height / 2 + i * 7), 2, Color.FromHex("#CCD3E8"));
    }
}

/// <summary>Allows the split to size a pane independently of its children's preferred width.</summary>
public sealed class ChatSplitPane : Control
{
    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        foreach (var child in Children) child.Measure(availableSize);
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        foreach (var child in Children)
            child.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        return finalSize;
    }
}
