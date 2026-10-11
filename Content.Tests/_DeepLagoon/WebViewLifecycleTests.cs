using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Robust.Client.WebView;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class WebViewLifecycleTests
{
    [Test]
    public void AlreadyClosedCefBrowserCanBeClosedAgainDuringUiTeardown()
    {
        // Exercise the compiled CEF implementation without creating a browser/GPU context.
        // A null live-data field is the exact state left by WebViewManagerCef.Shutdown.
        var type = typeof(WebViewControl).Assembly.GetType("Robust.Client.WebView.Cef.WebViewManagerCef+ControlImpl", true)!;
        var control = RuntimeHelpers.GetUninitializedObject(type);
        var close = type.GetMethod("CloseBrowser", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.DoesNotThrow(() => close.Invoke(control, null));
        Assert.DoesNotThrow(() => close.Invoke(control, null));
    }
}
