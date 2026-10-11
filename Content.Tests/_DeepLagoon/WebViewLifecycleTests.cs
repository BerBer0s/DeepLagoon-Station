using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Numerics;
using System.Text.Json;
using Content.Client._DeepLagoon.WebUI;
using NUnit.Framework;
using Robust.Client.WebView;
using Robust.Shared.Maths;
using Robust.Client.UserInterface;
using Xilium.CefGlue;

namespace Content.Tests._DeepLagoon;

[TestFixture]
public sealed class WebViewLifecycleTests
{
    [Test]
    public void DisposedControlsStillReturnANonEmptyCefViewRectangle()
    {
        var assembly = typeof(WebViewControl).Assembly;
        var owner = (WebViewControl)RuntimeHelpers.GetUninitializedObject(typeof(WebViewControl));
        typeof(Control).GetProperty("Disposed")!.SetValue(owner, true);
        var controlType = assembly.GetType("Robust.Client.WebView.Cef.WebViewManagerCef+ControlImpl", true)!;
        var control = RuntimeHelpers.GetUninitializedObject(controlType);
        controlType.GetField("Owner")!.SetValue(control, owner);
        var rendererType = assembly.GetType("Robust.Client.WebView.Cef.WebViewManagerCef+ControlRenderHandler", true)!;
        var renderer = RuntimeHelpers.GetUninitializedObject(rendererType);
        rendererType.GetField("_control", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(renderer, control);
        var arguments = new object[] { null, null };
        rendererType.GetMethod("GetViewRect", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(renderer, arguments);
        var rect = (CefRectangle)arguments[1];
        Assert.That(rect.Width, Is.GreaterThan(0));
        Assert.That(rect.Height, Is.GreaterThan(0));
    }

    [Test]
    public void DirtyPaintRegionsAccumulateUntilUploaded()
    {
        var type = typeof(WebViewControl).Assembly.GetType("Robust.Client.WebView.Cef.WebViewManagerCef+ControlRenderHandler", true)!;
        var renderer = RuntimeHelpers.GetUninitializedObject(type);
        var mark = type.GetMethod("MarkDirty", BindingFlags.NonPublic | BindingFlags.Instance)!;
        mark.Invoke(renderer, new object[] { 100, 100, new CefRectangle(20, 30, 10, 10) });
        mark.Invoke(renderer, new object[] { 100, 100, new CefRectangle(5, 50, 10, 10) });
        Assert.That(type.GetField("DirtyRegion", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer), Is.EqualTo(new UIBox2i(5, 30, 30, 60)));
        type.GetField("IsDirty", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(renderer, false);
        mark.Invoke(renderer, new object[] { 100, 100, new CefRectangle(1, 2, 3, 4) });
        Assert.That(type.GetField("DirtyRegion", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(renderer), Is.EqualTo(new UIBox2i(1, 2, 4, 6)));
    }

    [Test]
    public void RepeatedHeadshotUpdatesReplaceFieldsInsteadOfGrowingJson()
    {
        var data = new TguiData().String("name", "Test");
        for (var i = 0; i < 100; i++)
            data.Array("headshots", new[] { new TguiData().String("id", i.ToString()).String("image", "png") });
        using var json = JsonDocument.Parse(data.ToString());
        Assert.That(json.RootElement.EnumerateObject().Count(), Is.EqualTo(2));
        Assert.That(json.RootElement.GetProperty("headshots")[0].GetProperty("id").GetString(), Is.EqualTo("99"));
        Assert.That(data.ToString().Length, Is.LessThan(100));
    }

    [Test]
    public void FractionalWheelDeltasAccumulateAcrossEvents()
    {
        var type = typeof(WebViewControl).Assembly.GetType("Robust.Client.WebView.Cef.WebViewManagerCef+ControlImpl", true)!;
        var control = RuntimeHelpers.GetUninitializedObject(type);
        var consume = type.GetMethod("ConsumeWheel", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Vector2i Wheel(Vector2 delta) => (Vector2i)consume.Invoke(control, new object[] { delta })!;
        Assert.That(Wheel(new Vector2(0, 0.25f)), Is.EqualTo(new Vector2i(0, 12)));
        Assert.That(Wheel(new Vector2(0, 0.25f)), Is.EqualTo(new Vector2i(0, 13)));
        Assert.That(Wheel(new Vector2(0.01f, -0.25f)), Is.EqualTo(new Vector2i(0, -12)));
        Assert.That(Wheel(new Vector2(0.01f, -0.25f)), Is.EqualTo(new Vector2i(1, -13)));
    }

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
