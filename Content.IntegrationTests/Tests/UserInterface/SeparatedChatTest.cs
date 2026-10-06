using System.Numerics;
using Content.Client.UserInterface.Screens;
using Content.Client._DeepLagoon.WebUI;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Content.Client.Lobby.UI;
using System.Reflection;
using Content.Client.UserInterface.Systems.Chat.Controls;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class SeparatedChatTest
{
    [Test]
    public async Task LobbyUsesWebChatAndDoesNotBuildHiddenHistory()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var lobby = new LobbyGui();
            var web = lobby.Chat.WebChat;
            var contents = lobby.Chat.FindControl<ChatOutputPanel>("Contents");
            Assert.That(web, Is.Not.Null);
            Assert.That(web!.BrowserActive, Is.False, "Detached lobby must not create CEF.");
            Assert.That(contents.Visible, Is.False);
            var receive = typeof(ChatBox).GetMethod("OnMessageAdded", BindingFlags.Instance | BindingFlags.NonPublic)!;
            for (var i = 0; i < 1000; i++)
            {
                var message = new ChatMessage(ChatChannel.OOC, "message " + i, "message " + i, default, null);
                receive.Invoke(lobby.Chat, new object[] { message });
            }
            Assert.That(contents.EntryCount, Is.Zero,
                "TGUI messages must not also allocate hidden native rich-text rows.");
            lobby.Chat.Repopulate();
            Assert.That(contents.EntryCount, Is.Zero);
            lobby.CharacterPreview.SetLoaded(true);
            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080) })
            {
                // Resize updates the maximum information-pane height.
                for (var pass = 0; pass < 2; pass++)
                {
                    lobby.Measure(size);
                    lobby.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                }
                Assert.That(web.Width, Is.GreaterThan(0));
                Assert.That(web.Height, Is.GreaterThanOrEqualTo(120));
                Assert.That(web.GlobalPosition.Y + web.Height,
                    Is.LessThanOrEqualTo(lobby.Chat.ChatInput.GlobalPosition.Y));
                Assert.That(lobby.Chat.ChatInput.GlobalPosition.Y + lobby.Chat.ChatInput.Height,
                    Is.LessThanOrEqualTo(size.Y));
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PanelFollowsScreenWidth()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.Client.WaitPost(() =>
        {
            using var screen = new SeparatedChatGameScreen();
            var panel = screen.FindControl<PanelContainer>("SeparatedChatPanel");
            var viewport = screen.FindControl<LayoutContainer>("ViewportContainer");
            screen.SetChatSize(new Vector2(0.6f, 0));
            foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(2560, 1440) })
            {
                screen.Measure(size);
                screen.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                screen.SetChatPanelWidth(0.2f);
                screen.Measure(size);
                screen.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                Assert.That(panel.Width, Is.EqualTo(size.X * 0.2f - 6).Within(0.1f));
                Assert.That(panel.Height, Is.EqualTo(size.Y).Within(0.1f));
                Assert.That(viewport.Width, Is.EqualTo(size.X * 0.8f - 6).Within(0.1f));
                Assert.That(screen.ChatBox.ChatInput.Input.GlobalPosition.Y,
                    Is.LessThan(screen.ChatBox.ChatInput.ChannelSelector.GlobalPosition.Y));
                var tabs = screen.FindControl<PanelContainer>("ChatTabsPanel");
                Assert.That(tabs.Visible, Is.False);
                var web = screen.ChatBox.WebChat!;
                Assert.That(web.Width, Is.GreaterThan(0));
                Assert.That(web.Height, Is.GreaterThan(0));
                Assert.That(web.GlobalPosition.Y + web.Height,
                    Is.LessThanOrEqualTo(screen.ChatBox.ChatInput.GlobalPosition.Y));
                screen.SetChatPanelWidth(0.35f);
                screen.Measure(size);
                screen.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                Assert.That(panel.Width, Is.EqualTo(size.X * 0.35f - 6).Within(0.1f));
                Assert.That(viewport.Width + panel.Width + 12, Is.EqualTo(size.X).Within(0.1f));
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplicitSelectionOverridesDraftPrefix()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.Client.WaitPost(() =>
        {
            using var chat = new ChatBox();
            var selector = chat.ChatInput.ChannelSelector;
            selector.Select(ChatSelectChannel.OOC);
            chat.ChatInput.Input.SetText("[draft");
            selector.Select(ChatSelectChannel.LOOC);
            Assert.That(chat.SelectedChannel, Is.EqualTo(ChatSelectChannel.LOOC));
            Assert.That(chat.ChatInput.Input.Text, Is.EqualTo("draft"));

            // Selecting the stored channel must also override a different prefix.
            chat.ChatInput.Input.SetText("[second draft");
            selector.Select(ChatSelectChannel.LOOC);
            Assert.That(chat.ChatInput.Input.Text, Is.EqualTo("second draft"));
            Assert.That(selector.Text, Is.EqualTo(
                Content.Client.UserInterface.Systems.Chat.Controls.ChannelSelectorButton.ChannelSelectorName(ChatSelectChannel.LOOC)));
        });
        await pair.CleanReturnAsync();
    }
}
