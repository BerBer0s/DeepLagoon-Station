using System.Linq;
using Robust.Client.WebView;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Close through the UI tree lifecycle; CEF owns its buffers until OnBeforeClose.</summary>
public static class WebViewLifetime
{
    // A client-scoped service also isolates parallel integration clients.
    private static Dictionary<WebViewControl, Action> Active => IoCManager.Resolve<WebViewLifetimeRegistry>().Active;

    public static void Register(WebViewControl view, Action prepareForShutdown) => Active.Add(view, prepareForShutdown);

    /// <summary>Called by content EntryPoint before the engine's WebView shutdown.</summary>
    public static void Shutdown()
    {
        foreach (var (view, prepare) in Active.ToArray())
        {
            prepare();
            Close(view);
        }
    }

    public static void Close(WebViewControl view)
    {
        Active.Remove(view);
        view.ReleaseKeyboardFocus();
        view.AlwaysActive = false;
        view.Orphan();
    }
}

public sealed class WebViewLifetimeRegistry
{
    public readonly Dictionary<WebViewControl, Action> Active = new();
}
