using Content.Shared._DeepLagoon.WebUI;
using Robust.Client.UserInterface;
using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Reusable embedded TGUI: state replay, transport and lifecycle for every content window.</summary>
public sealed class TguiPanel : Control
{
    public GameWebView Web { get; }
    public event Action<string, string>? OnAction;
    public event Action? OnClose;
    private string _interface = "";
    private string _data = "{}";
    private string _title = "DeepLagoon";
    private IInputManager? _input;
    private BaseWindow? _dragWindow;
    private ScreenCoordinates _dragPointer;
    private Vector2 _dragOrigin;
    private bool _dragging;
    private string _chatState = "";
    private float _appearanceRefresh;

    public TguiPanel(bool suspendWhenHidden = false)
    {
        Web = new GameWebView(suspendWhenHidden: suspendWhenHidden);
        HorizontalExpand = VerticalExpand = true;
        AddChild(Web);
        Web.Ready += Publish;
        Web.Message += (type, payload) =>
        {
            if (type == "close") { OnClose?.Invoke(); return; }
            if (type.StartsWith("act/", StringComparison.Ordinal) && type.Length is > 4 and <= 68 &&
                payload.Length <= WebUiActionMessage.MaxPayloadLength)
                OnAction?.Invoke(type[4..], payload);
        };
    }

    public void SetState(string interfaceName, string dataJson, string title = "DeepLagoon")
    {
        if (_interface == interfaceName && _data == dataJson && _title == title) return;
        _interface = interfaceName;
        _data = dataJson;
        _title = title;
        Publish();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _input = IoCManager.Resolve<IInputManager>();
        _input.FirstChanceOnKeyEvent += OnWindowPointer;
    }

    protected override void ExitedTree()
    {
        EndWindowDrag();
        if (_input != null) _input.FirstChanceOnKeyEvent -= OnWindowPointer;
        _input = null;
        base.ExitedTree();
    }

    private BaseWindow? FloatingWindow()
    {
        for (Control? ancestor = Parent; ancestor != null; ancestor = ancestor.Parent)
            if (ancestor is BaseWindow window && window.Parent == UserInterfaceManager.WindowRoot)
                return window;
        return null;
    }

    private void OnWindowPointer(KeyEventArgs args, KeyEventType type)
    {
        if (args.Key != Keyboard.Key.MouseLeft || _input == null) return;
        if (type == KeyEventType.Up)
        {
            UpdateWindowDrag(_input.MouseScreenPosition);
            EndWindowDrag();
            return;
        }
        if (args.Handled || args.IsRepeat || type != KeyEventType.Down || !VisibleInTree || !Web.IsReady ||
            FloatingWindow() is not { } window) return;
        var pointer = _input.MouseScreenPosition;
        // Color wheels, sliders, selections and scrollbars own drags in the body.
        if (pointer.Position.Y / UIScale - GlobalPosition.Y > 32) return;
        for (var hit = UserInterfaceManager.MouseGetControl(pointer); hit != null; hit = hit.Parent)
        {
            if (hit != this) continue;
            BeginWindowDrag(window, pointer);
            break;
        }
    }

    private void BeginWindowDrag(BaseWindow window, ScreenCoordinates pointer)
    {
        _dragWindow = window;
        _dragPointer = pointer;
        _dragOrigin = window.Position;
        _dragging = false;
    }

    private void UpdateWindowDrag(ScreenCoordinates pointer)
    {
        if (_dragWindow is not { Parent: not null } window || pointer.Window != _dragPointer.Window) return;
        var delta = (pointer.Position - _dragPointer.Position) / UIScale;
        if (!_dragging)
        {
            // A click stays a click. Only actual movement turns it into a drag.
            if (delta.LengthSquared() < 16) return;
            _dragging = true;
            Web.SetWindowDragging(true);
            UserInterfaceManager.DeferAction(() =>
            {
                if (!window.Disposed && window.Parent != null) window.MoveToFront();
            });
        }
        LayoutContainer.SetPosition(window, Vector2.Clamp(_dragOrigin + delta,
            Vector2.Zero, Vector2.Max(Vector2.Zero, window.Parent!.Size - window.Size)));
    }

    private void EndWindowDrag()
    {
        var window = _dragWindow;
        if (_dragging)
        {
            Web.SetWindowDragging(false);
            // Mouse-up bindings and CEF can change focus after the first-chance
            // handler. Restore both native keyboard/text input and DOM focus
            // after that processing, without stealing it from another window.
            UserInterfaceManager.DeferAction(() =>
            {
                if (!Disposed && VisibleInTree && window is { Disposed: false, Parent: not null } &&
                    FloatingWindow() == window && window.IsAtFront())
                {
                    for (var captured = UserInterfaceManager.ControlFocused; captured != null; captured = captured.Parent)
                    {
                        if (captured != this) continue;
                        UserInterfaceManager.ControlFocused = null;
                        break;
                    }
                    Web.FocusTextInput();
                }
            });
        }
        _dragging = false;
        _dragWindow = null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _appearanceRefresh -= args.DeltaSeconds;
        if (_appearanceRefresh <= 0)
        {
            _appearanceRefresh = 0.2f;
            var preferences = IoCManager.Resolve<Content.Client.Lobby.IClientPreferencesManager>();
            var state = Content.Client.UserInterface.Systems.Chat.Controls.ChatTabsSettings.Deserialize(preferences.Preferences?.ChatPanelSettings ?? "")?.WebState ?? "";
            if (state != _chatState) { _chatState = state; Publish(); }
        }
        if (_dragWindow == null || _input == null) return;
        if (!VisibleInTree || FloatingWindow() != _dragWindow || !_input.IsKeyDown(Keyboard.Key.MouseLeft))
        {
            EndWindowDrag();
            return;
        }
        UpdateWindowDrag(_input.MouseScreenPosition);
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);
        if (!VisibleInTree) EndWindowDrag();
    }

    private void Publish()
    {
        if (_interface.Length == 0 || !Web.IsReady) return;
        Web.Send("update", "{\"config\":{\"interface\":" + GameWebView.Quote(_interface) +
            ",\"title\":" + GameWebView.Quote(_title) +
            ",\"status\":2,\"window\":{\"key\":\"deeplagoon\",\"fancy\":false}},\"data\":" + _data[..^1] +
            (_data.Trim() == "{}" ? "" : ",") + "\"chatState\":" + GameWebView.Quote(_chatState) + "}}");
    }
}
