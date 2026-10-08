using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Content.Shared._DeepLagoon.WebUI;
using Robust.Shared.Configuration;
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
    private WebViewControl _view;
    private readonly string _documentUrl;
    private readonly string? _devOrigin;
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly Queue<string> _outgoing = new();
    private bool _ready;
    private bool _disposed;
    private bool _messageDispatchPending;
    private bool _explicitTextInput;
    private readonly bool _suspendWhenHidden;
    private readonly bool _chat;
    private readonly NativeChatBackground? _background;
    public bool BrowserActive => _view.IsInsideTree;
    public bool IsReady => _ready;
    public event Action<string, string>? Message;
    public event Action? Ready;

    public void FocusInput() => _view.GrabKeyboardFocus();

    public bool HasInputFocus => _view.HasKeyboardFocus();

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

    public GameWebView(bool chat = false, bool suspendWhenHidden = false)
    {
        _chat = chat;
        _suspendWhenHidden = suspendWhenHidden;
        HorizontalExpand = VerticalExpand = true;
        if (chat)
        {
            _background = new NativeChatBackground();
            AddChild(_background);
        }
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
            // Reading or scrolling chat must not consume gameplay keys.
            // Editable DOM controls request keyboard focus through the bridge.
            KeyboardFocusOnClick = !_chat,
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
        if (context.Method != "GET" || uri.Query.Length > 65536 ||
            (context.RequestInitiator != "res://deeplagoon" && context.RequestInitiator != _devOrigin))
        {
            context.DoCancel();
            return;
        }
        // Query contains a percent-encoded message type and JSON payload.
        if (_incoming.Count < 64)
            _incoming.Enqueue(uri.Query.TrimStart('?'));
        context.DoRespondStream(new MemoryStream(Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7")), "image/gif");
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
        for (var i = 0; i < 32 && _incoming.TryDequeue(out var json); i++)
        {
            if (_disposed)
                return;
            var fields = json.Split('&', 2);
            if (fields.Length != 2) continue;
            var type = Uri.UnescapeDataString(fields[0]);
            var payload = Uri.UnescapeDataString(fields[1]);
            if (type.Length > 68 || payload.Length > 8192) continue;
            switch (type)
            {
                case "ready":
                    // Vite may reload the document after an HTML/non-component edit.
                    // Keep the native window open and let its owner resend current state.
                    // Force a fresh size notification once CEF has finished creating its view.
                    _view.Arrange(UIBox2.FromDimensions(System.Numerics.Vector2.Zero, Size + System.Numerics.Vector2.One));
                    InvalidateArrange();
                    _ready = true;
                    if (_background != null)
                        _view.ExecuteJavaScript("window.__deeplagoonNativeBackground = true; window.dispatchEvent(new Event('deeplagoon/native-background'));");
                    while (_outgoing.TryDequeue(out var queued))
                        Dispatch(queued);
                    Ready?.Invoke();
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

    protected override void ExitedTree()
    {
        base.ExitedTree();
        if (_suspendWhenHidden && !_disposed) SuspendBrowser();
        _ready = false;
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
        _background?.Reset();
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    private void SuspendBrowser()
    {
        ReleaseTextInput();
        _view.RemoveBeforeBrowseHandler(BeforeBrowse);
        _view.RemoveResourceRequestHandler(ResourceRequest);
        _view.Orphan();
        _view.Dispose();
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
        if (_view.Parent == null) _view.Dispose();
        base.Dispose(disposing);
    }
}
