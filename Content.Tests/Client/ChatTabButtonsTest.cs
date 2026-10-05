using System.Reflection;
using System.Runtime.CompilerServices;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Client.UserInterface.Systems.Chat;
using NUnit.Framework;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.IoC;
using Robust.UnitTesting;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ChatTabButtonsTest : RobustUnitTest
{
    public override UnitTestProject Project => UnitTestProject.Client;

    protected override void OverrideIoC()
    {
        base.OverrideIoC();
        var proxy = new Robust.Client.UserInterface.XAML.Proxy.XamlProxyManagerStub();
        IoCManager.Instance!.RegisterInstance(proxy.GetType().GetInterfaces()[0], proxy, overwrite: true);
    }

    [OneTimeSetUp]
    public void InitializeUi()
    {
        IoCManager.Resolve<IUserInterfaceManager>().InitializeTesting();
    }

    [Test]
    public void RebuildingTabsSelectsAnyIndexWithoutUnsettingGroupedButton()
    {
        // Exercise the real tab button builder with the engine UI, without
        // starting unrelated game systems or constructing the full chat box.
        var chat = (ChatBox) RuntimeHelpers.GetUninitializedObject(typeof(ChatBox));
        var settings = new ChatTabsSettings
        {
            Tabs = new() { new() { Name = "All" }, new() { Name = "Local" }, new() { Name = "OOC" } }
        };
        var container = new BoxContainer();
        SetField(chat, "_tabs", settings);
        SetField(chat, "_tabButtons", container);
        SetField(chat, "_addTab", new Button());
        SetField(chat, "_removeTab", new Button());
        var rebuild = typeof(ChatBox).GetMethod("RebuildTabButtons", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var index in new[] { 0, 1, 2, 0, 2, 1 })
        {
            settings.SelectedIndex = index;
            rebuild.Invoke(chat, null);
            var pressed = 0;
            for (var i = 0; i < container.ChildCount; i++)
            {
                var button = (Button) container.GetChild(i);
                Assert.That(button.Pressed, Is.EqualTo(i == index));
                if (button.Pressed)
                    pressed++;
            }
            Assert.That(pressed, Is.EqualTo(1));
        }
    }

    private static void SetField(ChatBox chat, string name, object value)
    {
        typeof(ChatBox).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(chat, value);
    }

    [Test]
    public void EditingTabNameSchedulesSaveWithoutEnter()
    {
        var chat = (ChatBox) RuntimeHelpers.GetUninitializedObject(typeof(ChatBox));
        var settings = new ChatTabsSettings { Tabs = new() { new() { Name = "Local" } } };
        chat.NameScope = new NameScope();
        var panel = new PanelContainer { Visible = false };
        var contents = new ChatOutputPanel();
        var body = new LayoutContainer();
        body.AddChild(panel);
        body.AddChild(contents);
        chat.NameScope.Register("SettingsPanel", panel);
        chat.NameScope.Register("Contents", contents);
        SetField(chat, "_controller", new ChatUIController());
        SetField(chat, "_tabs", settings);
        SetField(chat, "_tabButtons", new BoxContainer());
        typeof(ChatBox).GetMethod("OpenPanelSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chat, null);
        var input = (LineEdit) typeof(ChatBox).GetField("_tabName", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chat)!;
        input.SetText("Моя вкладка", invokeEvent: true);
        Assert.That(ChatTabsSettings.Deserialize(settings.Serialize())!.Tabs[0].Name, Is.EqualTo("Моя вкладка"));
        Assert.That((float) typeof(ChatBox).GetField("_settingsSaveDelay", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(chat)!, Is.GreaterThan(0));
        Assert.That(panel.Parent, Is.SameAs(contents.Parent));
        Assert.That(panel.Visible, Is.True);
        Assert.That(panel.ChildCount, Is.EqualTo(1));
        typeof(ChatBox).GetMethod("ClosePanelSettings", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(chat, null);
        Assert.That(panel.Visible, Is.False);
    }
}
