using System.Reflection;
using Content.Client._DeepLagoon.WebUI;
using Robust.Client.UserInterface;
using Robust.Client.WebView;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._DeepLagoon;

[TestFixture]
public sealed class WebViewWarmLifetimeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public async Task ShortHidePreservesControlAndReadyStateButExpiryReleasesIt()
    {
        if (!WebViewCapabilities.NativeLifecycle)
            Assert.Ignore("The official module uses the separately tested close/recreate fallback.");
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true });
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            using var web = new GameWebView(suspendWhenHidden: true);
            ui.WindowRoot.AddChild(web);
            var frame = typeof(GameWebView).GetMethod("FrameUpdate", Private)!;
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            var view = (WebViewControl)typeof(GameWebView).GetField("_view", Private)!.GetValue(web)!;
            Assert.That(view.AlwaysActive, Is.True);
            // Headless never executes the document: simulate only its Ready handshake.
            typeof(GameWebView).GetField("_ready", Private)!.SetValue(web, true);
            web.Visible = false;
            web.Send("update", "{\"data\":{\"name\":\"latest\"}}");
            Assert.That(view.Parent, Is.SameAs(web));
            Assert.That(web.IsReady, Is.True);
            web.Visible = true;
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            Assert.That(typeof(GameWebView).GetField("_warmSuspended", Private)!.GetValue(web), Is.False);
            Assert.That(typeof(GameWebView).GetField("_view", Private)!.GetValue(web), Is.SameAs(view));
            web.Orphan();
            Assert.That(web.IsReady, Is.True, "Removing a retained editor from the UI tree must preserve its document");
            ui.WindowRoot.AddChild(web);
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            Assert.That(typeof(GameWebView).GetField("_warmSuspended", Private)!.GetValue(web), Is.False);
            web.Visible = false;
            typeof(GameWebView).GetField("_warmUntil", Private)!.SetValue(web, TimeSpan.MinValue);
            typeof(GameWebView).GetMethod("CheckWarmExpiry", Private)!.Invoke(web, null);
            Assert.That(web.IsReady, Is.False);
            Assert.That(view.Parent, Is.Null);
            Assert.That(view.AlwaysActive, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StockModuleHidingClosesAndRecreatesBrowserWithoutForkApi()
    {
        Assert.That(WebViewCapabilities.NativeLifecycle, Is.False, "Production tests must use the stock client module");
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { InLobby = true, Destructive = true });
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            using var web = new GameWebView(suspendWhenHidden: true);
            ui.WindowRoot.AddChild(web);
            var frame = typeof(GameWebView).GetMethod("FrameUpdate", Private)!;
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            var field = typeof(GameWebView).GetField("_view", Private)!;
            var first = (WebViewControl)field.GetValue(web)!;
            Assert.That(first.AlwaysActive, Is.False);
            typeof(GameWebView).GetField("_ready", Private)!.SetValue(web, true);
            web.Visible = false;
            Assert.That(first.Parent, Is.Null);
            Assert.That(web.IsReady, Is.False);
            Assert.That(field.GetValue(web), Is.Not.SameAs(first));
            web.Visible = true;
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            Assert.That(web.BrowserActive, Is.True);
            // Mirrors the official engine's mod-loader -> CEF -> entities order.
            WebViewLifetime.Shutdown();
            Assert.That(web.BrowserActive, Is.False);
            Assert.That(web.IsReady, Is.False);
            frame.Invoke(web, new object[] { new FrameEventArgs(0) });
            Assert.That(web.BrowserActive, Is.False, "Shutdown cannot recreate a browser");
            Assert.DoesNotThrow(() => web.Orphan());
        });
        await pair.CleanReturnAsync();
    }
}
