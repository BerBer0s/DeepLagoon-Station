using System;
using System.Numerics;
using System.Reflection;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.WebView;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.UnitTesting;

namespace Content.Tests.Client;

[TestFixture]
public sealed class WebViewChatMouseTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    [OneTimeSetUp]
    public void InitializeWebView()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
        var type = typeof(WebViewControl).Assembly.GetType("Robust.Client.WebView.WebViewManager", true)!;
        var manager = Activator.CreateInstance(type, true)!;
        var initialize = type.GetMethod("PreInitialize")!;
        var headless = Enum.Parse(initialize.GetParameters()[1].ParameterType, "Headless");
        initialize.Invoke(manager, new object[] { IoCManager.Instance!, headless });
        IoCManager.Instance!.BuildGraph();
    }

    [TestCase("UIClick", false)]
    [TestCase("UIClick", true)]
    [TestCase("UIRightClick", false)]
    [TestCase("UIRightClick", true)]
    public void ChatMousePressAndReleaseAreDeliveredWithoutKeyboardFocus(string function, bool up)
    {
        using var view = new WebViewControl { KeyboardFocusOnClick = false };
        var args = new GUIBoundKeyEventArgs(new BoundKeyFunction(function), up ? BoundKeyState.Up : BoundKeyState.Down,
            default, true, new Vector2(4, 5), new Vector2(8, 10));
        typeof(WebViewControl).GetMethod(up ? "KeyBindUp" : "KeyBindDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, new object[] { args });
        Assert.Multiple(() =>
        {
            Assert.That(args.Handled, Is.True);
            Assert.That(view.HasKeyboardFocus(), Is.False);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ChatDoesNotConsumeMovementKeys(bool up)
    {
        using var view = new WebViewControl { KeyboardFocusOnClick = false };
        var args = new GUIBoundKeyEventArgs(EngineKeyFunctions.MoveUp, up ? BoundKeyState.Up : BoundKeyState.Down,
            default, false, Vector2.Zero, Vector2.Zero);
        typeof(WebViewControl).GetMethod(up ? "KeyBindUp" : "KeyBindDown", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(view, new object[] { args });
        Assert.That(args.Handled, Is.False);
    }
}
