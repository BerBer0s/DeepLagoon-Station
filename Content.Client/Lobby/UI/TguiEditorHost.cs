using System.Numerics;
using Content.Client._DeepLagoon.WebUI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Lobby.UI;

/// <summary>Keep the browser in one parent; tabs only select the content under it.</summary>
public sealed class TguiEditorHost : Container
{
    private readonly TabContainer _tabs;
    private readonly TguiPanel _panel;
    private readonly Control _sprite;

    public TguiEditorHost(TabContainer tabs, TguiPanel panel, Control sprite)
    {
        HorizontalExpand = VerticalExpand = true;
        _tabs = tabs;
        _panel = panel;
        _sprite = sprite;
        AddChild(tabs);
        AddChild(panel);
        AddChild(sprite);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _tabs.Measure(availableSize);
        _panel.Measure(availableSize);
        _sprite.Measure(new Vector2(232, 320));
        // Specialized native tabs may have large minimum sizes. The editor is
        // a viewport: those sizes must not resize the lobby/chat when switching.
        return Vector2.Zero;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _tabs.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        _panel.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
        _sprite.Arrange(UIBox2.FromDimensions(new Vector2(24, 30), new Vector2(232, 320)));
        return finalSize;
    }
}
