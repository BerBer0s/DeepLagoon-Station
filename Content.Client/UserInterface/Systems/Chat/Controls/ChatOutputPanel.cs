using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

/// <summary>Scrollable chat history with a separate translucent surface for each message.</summary>
public sealed class ChatOutputPanel : ScrollContainer
{
    private readonly BoxContainer _rows;
    private bool _following = true;
    private Color _messageBackground;
    public int EntryCount => _rows.ChildCount;

    public ChatOutputPanel()
    {
        HScrollEnabled = false;
        ReserveScrollbarSpace = true;
        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3, HorizontalExpand = true
        };
        AddChild(_rows);
        OnScrolled += () => _following = VScroll >= MathF.Max(0, _rows.Height - Height) - 2;
        MessageBackground = SurfaceColor(Color.FromHex("#20222E"));
    }

    public static Color SurfaceColor(Color background) => new(
        background.R + (1 - background.R) * 0.18f,
        background.G + (1 - background.G) * 0.18f,
        background.B + (1 - background.B) * 0.18f, 0.72f);

    public Color MessageBackground
    {
        get => _messageBackground;
        set
        {
            _messageBackground = value;
            foreach (var row in _rows.Children) ((PanelContainer) row).PanelOverride = MakeStyle();
        }
    }

    private StyleBoxFlat MakeStyle() => new()
    {
        BackgroundColor = _messageBackground,
        ContentMarginLeftOverride = 5, ContentMarginRightOverride = 5,
        ContentMarginTopOverride = 3, ContentMarginBottomOverride = 3
    };

    public void AddMessage(FormattedMessage message, Type[]? tagsAllowed = null)
    {
        var row = new PanelContainer { HorizontalExpand = true, PanelOverride = MakeStyle() };
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(message, tagsAllowed);
        row.AddChild(label);
        _rows.AddChild(row);
    }

    public FormattedMessage GetMessage(Index index) =>
        ((RichTextLabel) _rows.GetChild(index.GetOffset(EntryCount)).GetChild(0)).GetFormattedMessage()!;

    public void RemoveEntry(Index index) => _rows.RemoveChild(_rows.GetChild(index.GetOffset(EntryCount)));

    public void Clear()
    {
        _rows.RemoveAllChildren();
        VScroll = 0;
        _following = true;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        // Preserve follow mode while the scrollbar range changes during layout.
        var following = _following && VScrollTarget >= MathF.Max(0, _rows.Height - Height) - 2;
        _following = following;
        var result = base.ArrangeOverride(finalSize);
        if (!following) return result;
        VScroll = float.MaxValue;
        _following = true;
        return base.ArrangeOverride(finalSize);
    }
}
