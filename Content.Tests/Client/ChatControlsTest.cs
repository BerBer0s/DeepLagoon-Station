using System.Numerics;
using Content.Client.UserInterface.Systems.Chat.Controls;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.UnitTesting;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ChatControlsTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;
    [OneTimeSetUp]
    public void InitializeUi() => IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();

    [Test]
    public void WheelAndBrightnessApplyOnEachChange()
    {
        var picker = new ChatColorPicker(Color.Red);
        picker.Wheel.Measure(new Vector2(150));
        picker.Wheel.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(150)));
        var changes = 0;
        var last = Color.Transparent;
        picker.OnColorChanged += color => { changes++; last = color; };
        picker.Wheel.SelectAt(new Vector2(75));
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(last.R, Is.EqualTo(last.G).Within(0.01));
        picker.Brightness.Value = 0.3f;
        Assert.That(changes, Is.EqualTo(2));
        Assert.That(last.R, Is.EqualTo(0.3f).Within(0.01));
        picker.Opacity.Value = 0.5f;
        Assert.That(last.A, Is.EqualTo(0.5f).Within(0.01));
    }

    [Test]
    public void EveryMessageHasItsOwnTranslucentSurfaceAndKeepsMarkup()
    {
        var history = new ChatOutputPanel();
        var background = Color.FromHex("#20222E");
        history.MessageBackground = ChatOutputPanel.SurfaceColor(background);
        history.AddMessage(FormattedMessage.FromMarkupOrThrow("[bold]Первое сообщение[/bold]"));
        history.AddMessage(FormattedMessage.FromUnformatted("Второе сообщение"));
        Assert.That(history.EntryCount, Is.EqualTo(2));
        Assert.That(history.GetMessage(0).ToMarkup(), Does.Contain("[bold]"));
        Assert.Multiple(() =>
        {
            Assert.That(history.MessageBackground.R, Is.GreaterThan(background.R));
            Assert.That(history.MessageBackground.G, Is.GreaterThan(background.G));
            Assert.That(history.MessageBackground.B, Is.GreaterThan(background.B));
            Assert.That(history.MessageBackground.A, Is.InRange(0.01f, 0.99f));
        });
        history.RemoveEntry(^2);
        Assert.That(history.GetMessage(0).ToMarkup(), Is.EqualTo("Второе сообщение"));
        history.Clear();
        Assert.That(history.EntryCount, Is.Zero);
    }
}
