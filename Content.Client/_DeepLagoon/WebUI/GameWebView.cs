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
/// dispatch messages from FrameUpdate. Remote documents and downloads are rejected.
/// </summary>
public sealed class GameWebView : Control
{
    private const string Root = "/Web/DeepLagoon/";
    private readonly WebViewControl _view;
    private readonly string? _devOrigin;
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly Queue<string> _outgoing = new();
    private bool _ready;
    private bool _disposed;
    private readonly bool _suspendWhenHidden;
    public bool BrowserActive => _view.IsInsideTree;
    public bool IsReady => _ready;
    public event Action<string, string>? Message;
    public event Action? Ready;

    public GameWebView(bool chat = false, bool suspendWhenHidden = false)
    {
        _suspendWhenHidden = suspendWhenHidden;
        HorizontalExpand = VerticalExpand = true;
#if DEBUG
        _devOrigin = TguiDevelopmentPolicy.GetOrigin(IoCManager.Resolve<IConfigurationManager>().GetCVar(WebUiCVars.DevServer));
#endif
        _view = new WebViewControl
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Url = (_devOrigin == null ? "res://deeplagoon" + Root : _devOrigin + "/") +
                  (chat ? "chat.html" : "interface.html")
        };
        _view.AddBeforeBrowseHandler(BeforeBrowse);
        _view.AddResourceRequestHandler(ResourceRequest);
        if (!_suspendWhenHidden) AddChild(_view);
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
                    _ready = true;
                    while (_outgoing.TryDequeue(out var queued))
                        Dispatch(queued);
                    Ready?.Invoke();
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
        if (_suspendWhenHidden && _view.Parent != null) _view.Orphan();
        _ready = false;
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    protected override void VisibilityChanged(bool newVisible)
    {
        base.VisibilityChanged(newVisible);
        if (!_suspendWhenHidden || VisibleInTree || _view.Parent == null) return;
        _view.Orphan();
        _ready = false;
        _outgoing.Clear();
        while (_incoming.TryDequeue(out _)) { }
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        _view.RemoveBeforeBrowseHandler(BeforeBrowse);
        _view.RemoveResourceRequestHandler(ResourceRequest);
        if (_view.Parent == null) _view.Dispose();
        base.Dispose(disposing);
    }
}
