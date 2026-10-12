using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.WebUI;

/// <summary>
/// TEMPORARY engine workaround (K1). A browser closes asynchronously when its control leaves the UI tree,
/// and CEF keeps asking the control for its view rect for a moment afterwards. The engine answers a disposed
/// control with an empty rect, which crashes libcef (0x80000003 was observed when a window was opened and
/// closed quickly). So a browser control leaves the tree now, which closes its browser, and is disposed later.
/// Every browser control goes through this: <see cref="GameWebView"/> and the wiki window.
/// Remove once the engine answers a disposed control with a 1x1 rect.
/// </summary>
public static class WebViewRelease
{
    public const int DisposeDelayMs = 3000;

    public static void CloseNowDisposeLater(Control control)
    {
        if (control.Disposed)
            return;

        control.Orphan();
        Timer.Spawn(DisposeDelayMs, () =>
        {
#pragma warning disable CS0618
            if (!control.Disposed)
                control.Dispose();
#pragma warning restore CS0618
        });
    }
}
