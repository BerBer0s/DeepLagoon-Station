using Robust.Client.WebView;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Old official modules and the DeepLagoon module use the same content API.</summary>
public static class WebViewCapabilities
{
    public static readonly bool NativeLifecycle = DetectNativeLifecycle();

    private static bool DetectNativeLifecycle()
    {
        // GetManifestResourceStream is allowed by the stock engine's content sandbox.
        using var capabilities = typeof(WebViewControl).Assembly
            .GetManifestResourceStream("Robust.Client.WebView.NativeLifecycle.v1");
        return capabilities != null;
    }
}
