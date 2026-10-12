using Robust.Client.WebView;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Close through the UI tree lifecycle; CEF owns its buffers until OnBeforeClose.</summary>
public static class WebViewLifetime
{
    public static void Close(WebViewControl view)
    {
        view.ReleaseKeyboardFocus();
        view.AlwaysActive = false;
        view.Orphan();
    }
}
