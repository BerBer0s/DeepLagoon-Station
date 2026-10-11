using Robust.Client.WebView;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Keep the stock engine's view rectangle alive during asynchronous CEF close.</summary>
public static class WebViewLifetime
{
    public static void Close(WebViewControl view)
    {
        view.ReleaseKeyboardFocus();
        view.Orphan();
        // An ancestor may already have removed it from the tree.
        Timer.Spawn(3000, () =>
        {
#pragma warning disable CS0618
            if (!view.Disposed) view.Dispose();
#pragma warning restore CS0618
        });
    }
}
