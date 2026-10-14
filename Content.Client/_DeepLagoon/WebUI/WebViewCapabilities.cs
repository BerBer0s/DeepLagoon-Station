using Robust.Client.WebView;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>Old official modules and the DeepLagoon module use the same content API.</summary>
public static class WebViewCapabilities
{
    public static readonly bool NativeLifecycle = DetectNativeLifecycle();

    private static bool DetectNativeLifecycle()
    {
        // GetManifestResourceStream is allowed by the stock engine's content sandbox.
        return Detect("Robust.Client.WebView.NativeLifecycle.v1");
    }

    private static bool Detect(string resource)
    {
        using var capabilities = typeof(WebViewControl).Assembly.GetManifestResourceStream(resource);
        return capabilities != null;
    }
}
