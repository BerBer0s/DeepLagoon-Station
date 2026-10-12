using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Content.Shared._DeepLagoon.WebUI;
using Robust.Shared.Configuration;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.WebView;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>
/// Local packaged web UI. CEF callbacks run on another thread: only enqueue here;
/// dispatch messages after the UI frame traversal. Remote documents and downloads are rejected.
/// </summary>
public sealed class GameWebView : Control
{
    private const string Root = "/Web/DeepLagoon/";

    // Page -> C# limits, in one unit: UTF-16 code units of the decoded message type and payload JSON,
    // the same strings bridge.js measures. The encoded query only gets a cap derived from them
    // (encodeURIComponent turns one UTF-16 code unit into at most 9 characters).
    public const int MaxMessageTypeLength = 68;
    public const int MaxMessagePayloadLength = WebUiActionMessage.MaxPayloadLength;
    private const int MaxQueryLength = (MaxMessageTypeLength + MaxMessagePayloadLength) * 9 + 2;
    private const int MaxIncoming = 64;
    private WebViewControl _view;
    private readonly string _documentUrl;
    private readonly string? _devOrigin;
    private readonly ConcurrentQueue<(string Type, string Payload)> _incoming = new();
    private readonly Queue<string> _outgoing = new();
    private bool _ready;
    private bool _disposed;
    private bool _messageDispatchPending;
    private bool _explicitTextInput;
    // Interface pages report whether a text field has DOM focus (bridge.js, "input-focus").
    private bool _pageEditing;
    // FocusTextInput requests, and their count when the edited field got focus (see "input-focus").
    private int _textInputRequests;
    private int _editingRequests;
    private IInputManager? _input;
    private readonly bool _suspendWhenHidden;
    private readonly bool _chat;
    private readonly NativeChatBackground _background;
    public bool BrowserActive => _view.IsInsideTree;
    public bool IsReady => _ready;
    public event Action<string, string>? Message;
    public event Action? Ready;

    public void FocusInput() => _view.GrabKeyboardFocus();

    public bool HasInputFocus => _view.HasKeyboardFocus();

    /// <summary>Text input was asked for explicitly (chat, composer, a page's "focus" action).</summary>
    public bool HasTextInput => _explicitTextInput;

    /// <summary>A text field of the page has DOM focus.</summary>
    public bool IsEditing => _pageEditing;

    public void SendGridEditorKey(string code, bool down, bool control, bool shift, bool alt)
    {
        if (!_ready || _disposed) return;
        var detail = new TguiData().String("code", code).Bool("down", down).Bool("ctrlKey", control)
            .Bool("shiftKey", shift).Bool("altKey", alt).ToString();
        _view.ExecuteJavaScript("window.dispatchEvent(new CustomEvent('grid-editor-key',{detail:" + detail + "}));");
    }

    public void SetWindowDragging(bool dragging)
    {
        if (!_ready || _disposed) return;
        _view.ExecuteJavaScript("window.dispatchEvent(new CustomEvent('deeplagoon/window-drag', {detail:" +
            (dragging ? "true" : "false") + "}));");
    }

    public void FocusTextInput()
    {
        FocusInput();
        _explicitTextInput = true;
        _textInputRequests++;
        _view.Root?.Window?.TextInputStart();
        if (_ready)
            _view.ExecuteJavaScript("window.focus(); window.dispatchEvent(new Event('deeplagoon/focus-input'));");
    }

    // The current CEF adapter follows Return with a Backspace char event.
    // Composer intercepts that key and sends a DOM keydown without the bad char.
    public void SendInputKey(bool tab, bool shift)
    {
        if (!_ready || _disposed || !HasInputFocus) return;
        _view.ExecuteJavaScript("document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', {" +
            (tab ? "key:'Tab',code:'Tab',keyCode:9,which:9," : "key:'Enter',code:'Enter',keyCode:13,which:13,") +
            "bubbles:true,cancelable:true,shiftKey:" +
            (shift ? "true" : "false") + "}));");
    }

    public void ReleaseTextInput()
    {
        if (_explicitTextInput && HasInputFocus) _view.Root?.Window?.TextInputStop();
        _explicitTextInput = false;
    }

    public void PrepareForShutdown()
    {
        // CEF has already stopped when entity systems shut down. UI teardown
        // (including ending a window drag) must not execute more JavaScript.
        ReleaseTextInput();
        _ready = false;
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    public GameWebView(bool chat = false, bool suspendWhenHidden = false)
    {
        _chat = chat;
        _suspendWhenHidden = suspendWhenHidden;
        HorizontalExpand = VerticalExpand = true;
        // Interfaces such as the character editor use the same native layer.
        // It draws nothing until the document publishes an enabled preset.
        _background = new NativeChatBackground();
        AddChild(_background);
#if DEBUG
        _devOrigin = TguiDevelopmentPolicy.GetOrigin(IoCManager.Resolve<IConfigurationManager>().GetCVar(WebUiCVars.DevServer));
#endif
        _documentUrl = (_devOrigin == null ? "res://deeplagoon" + Root : _devOrigin + "/") +
            (chat ? "chat.html" : "interface.html");
        _view = CreateView();
        if (!_suspendWhenHidden) AddChild(_view);
    }

    private WebViewControl CreateView()
    {
        var view = new WebViewControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            // Stock WebView delivers mouse clicks through keyboard-focused raw
            // input. The chat bridge releases focus after non-editable clicks.
            KeyboardFocusOnClick = true,
            Url = _documentUrl
        };
        view.AddBeforeBrowseHandler(BeforeBrowse);
        view.AddResourceRequestHandler(ResourceRequest);
        return view;
    }

    private static bool IsLocal(Uri uri) => uri.Scheme == "res" &&
        uri.Host == "deeplagoon" && uri.AbsolutePath.StartsWith(Root, StringComparison.Ordinal);

    private void BeforeBrowse(IBeforeBrowseContext context)
    {
        if (!WikiNavigationPolicy.TryUri(context.Url, out var uri) ||
            (!IsLocal(uri) && !TguiDevelopmentPolicy.IsDocument(uri, _devOrigin)))
            context.DoCancel();
    }

    private void ResourceRequest(IRequestHandlerContext context)
    {
        // Bundles embed fonts and images as data URIs; these have no network access.
        if (!context.IsNavigation && !context.IsDownload && context.Url.StartsWith("data:", StringComparison.Ordinal))
            return;
        if (!WikiNavigationPolicy.TryUri(context.Url, out var uri) || context.IsDownload ||
            (!IsLocal(uri) && !TguiDevelopmentPolicy.IsResource(uri, _devOrigin)))
        {
            context.DoCancel();
            return;
        }
        if (uri.AbsolutePath != Root + "bridge")
            return;
        // A message that is not taken (too long, queue full) gets no image: the page's onerror reports it.
        if (context.Method != "GET" || uri.Query.Length > MaxQueryLength ||
            (context.RequestInitiator != "res://deeplagoon" && context.RequestInitiator != _devOrigin) ||
            !TryDecodeMessage(uri.Query, out var message) || _incoming.Count >= MaxIncoming)
        {
            context.DoCancel();
            return;
        }
        _incoming.Enqueue(message);
        context.DoRespondStream(new MemoryStream(Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7")), "image/gif");
    }

    // Query contains a percent-encoded message type and JSON payload.
    private static bool TryDecodeMessage(string query, out (string Type, string Payload) message)
    {
        message = default;
        var fields = query.TrimStart('?').Split('&', 2);
        if (fields.Length != 2) return false;
        message = (Uri.UnescapeDataString(fields[0]), Uri.UnescapeDataString(fields[1]));
        return message.Type.Length <= MaxMessageTypeLength && message.Payload.Length <= MaxMessagePayloadLength;
    }

    public void Send(string type, string payloadJson)
    {
        var json = "{\"type\":" + Quote(type) + ",\"payload\":" + payloadJson + "}";
        if (!_ready)
        {
            if (_outgoing.Count < 256)
                _outgoing.Enqueue(json);
            return;
        }
        Dispatch(json);
    }

    public void Reload()
    {
        _ready = false;
        _background?.Reset();
        _outgoing.Clear();
        _view.Reload();
    }

    private void Dispatch(string json) => _view.ExecuteJavaScript("try { window.update(" +
        Quote(json) + "); } catch (error) { console.error(error); }");

    /// <summary>JSON string encoding without adding non-sandboxed client dependencies.</summary>
    public static string Quote(string value)
    {
        var builder = new StringBuilder("\"");
        foreach (var c in value)
        {
            if (c == '\\' || c == '"') builder.Append('\\').Append(c);
            else if (c < ' ' || c == '\u2028' || c == '\u2029')
                builder.Append("\\u").Append(((int)c).ToString("x4"));
            else builder.Append(c);
        }
        return builder.Append('"').ToString();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        // Attach only once the visible viewport is laid out. Hidden editors do
        // not create background CEF browsers, even while their UI stays in tree.
        if (_suspendWhenHidden && VisibleInTree && _view.Parent == null) AddChild(_view);
        if (_disposed || _messageDispatchPending || _incoming.IsEmpty) return;
        _messageDispatchPending = true;
        // Actions can add or remove windows (including this browser's owner).
        // Never invoke them while an ancestor enumerates its child controls.
        UserInterfaceManager.DeferAction(() =>
        {
            _messageDispatchPending = false;
            DispatchIncoming();
        });
    }

    private void DispatchIncoming()
    {
        for (var i = 0; i < 32 && _incoming.TryDequeue(out var message); i++)
        {
            if (_disposed)
                return;
            var (type, payload) = message;
            switch (type)
            {
                case "ready":
                    // Vite may reload the document after an HTML/non-component edit.
                    // Keep the native window open and let its owner resend current state.
                    // Force a fresh size notification once CEF has finished creating its view.
                    _view.Arrange(UIBox2.FromDimensions(System.Numerics.Vector2.Zero, Size + System.Numerics.Vector2.One));
                    InvalidateArrange();
                    _ready = true;
                    _pageEditing = false;
                    if (_background != null)
                        _view.ExecuteJavaScript("window.__deeplagoonNativeBackground = true; window.dispatchEvent(new Event('deeplagoon/native-background'));");
                    while (_outgoing.TryDequeue(out var queued))
                        Dispatch(queued);
                    Ready?.Invoke();
                    break;
                case "input-focus":
                    if (_chat || !TguiActionData.TryParse(payload, out var editing)) break;
                    var active = editing!.String("active") == "true";
                    if (active && !_pageEditing)
                        _editingRequests = _textInputRequests;
                    // Text input asked for while this field was focused ends with the field; a newer request
                    // (a click elsewhere in the page that asked for the keyboard) stays.
                    else if (!active && _pageEditing && _textInputRequests == _editingRequests)
                        ReleaseTextInput();
                    _pageEditing = active;
                    break;
                case "native-background":
                    _background?.Configure(payload);
                    break;
                case "chat-input-focus":
                    if (!_chat || !VisibleInTree || !_view.IsInsideTree ||
                        !TguiActionData.TryParse(payload, out var focus)) break;
                    if (focus!.String("active") == "true")
                    {
                        FocusInput();
                        _explicitTextInput = true;
                        _view.Root?.Window?.TextInputStart();
                    }
                    else if (focus.String("active") == "false")
                    {
                        ReleaseTextInput();
                        // Do not release another window's focus (e.g. the composer).
                        _view.ReleaseKeyboardFocus();
                    }
                    break;
                default:
                    Message?.Invoke(type, payload);
                    break;
            }
        }
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _input = IoCManager.Resolve<IInputManager>();
        _input.FirstChanceOnKeyEvent += OnFirstChanceKey;
    }

    // Stock WebView keeps every key while it has keyboard focus (its RawKeyEvent returns true), so one
    // click into a window took WASD away from the game until the player clicked the world. First-chance
    // handlers run before that raw dispatch: while no text field is edited and no text input was asked
    // for, a key the page does not use itself releases the focus here and goes on to the game binds.
    // The page keeps TGUI's own keys (hotkeys.ts): Esc, Enter, Space, Tab, arrows, F5, modifiers, Ctrl combos.
    private void OnFirstChanceKey(KeyEventArgs args, KeyEventType type)
    {
        if (args.Handled || type != KeyEventType.Down || _explicitTextInput || _pageEditing ||
            IsPageKey(args) || !_view.HasKeyboardFocus())
            return;
        _view.ReleaseKeyboardFocus();
    }

    private static bool IsPageKey(KeyEventArgs args) => args.Control || args.Key.IsMouseKey() || args.Key is
        Keyboard.Key.Escape or Keyboard.Key.Return or Keyboard.Key.NumpadEnter or Keyboard.Key.Space or
        Keyboard.Key.Tab or Keyboard.Key.Up or Keyboard.Key.Down or Keyboard.Key.Left or Keyboard.Key.Right or
        Keyboard.Key.F5 or Keyboard.Key.Control or Keyboard.Key.Shift or Keyboard.Key.Alt or
        Keyboard.Key.LSystem or Keyboard.Key.RSystem;

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_input != null) _input.FirstChanceOnKeyEvent -= OnFirstChanceKey;
        _input = null;
        if (_suspendWhenHidden && !_disposed) SuspendBrowser();
        _ready = false;
        _pageEditing = false;
        _background?.Reset();
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);
        if (!_suspendWhenHidden || VisibleInTree || _view.Parent == null) return;
        SuspendBrowser();
        _ready = false;
        _pageEditing = false;
        _background?.Reset();
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    private void SuspendBrowser()
    {
        ReleaseTextInput();
        _view.RemoveBeforeBrowseHandler(BeforeBrowse);
        _view.RemoveResourceRequestHandler(ResourceRequest);
        WebViewRelease.CloseNowDisposeLater(_view);
        // StartBrowser allocates a 1x1 texture. A fresh control guarantees that
        // layout invokes Resized even when the resumed viewport has the same size.
        _view = CreateView();
    }

    protected override void Dispose(bool disposing)
    {
        ReleaseTextInput();
        _disposed = true;
        _view.RemoveBeforeBrowseHandler(BeforeBrowse);
        _view.RemoveResourceRequestHandler(ResourceRequest);
        WebViewRelease.CloseNowDisposeLater(_view);
        base.Dispose(disposing);
    }
}
