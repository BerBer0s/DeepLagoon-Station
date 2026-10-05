using System.Numerics;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class SeparatedChatTest
{
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
                Assert.That(panel.Width, Is.EqualTo(size.X * 0.2f).Within(0.1f));
                Assert.That(panel.Height, Is.EqualTo(size.Y).Within(0.1f));
                Assert.That(viewport.Width, Is.EqualTo(size.X * 0.8f).Within(0.1f));
                Assert.That(screen.ChatBox.ChatInput.Input.GlobalPosition.Y,
                    Is.LessThan(screen.ChatBox.ChatInput.ChannelSelector.GlobalPosition.Y));
                var tabs = screen.FindControl<PanelContainer>("ChatTabsPanel");
                Assert.That(tabs.Height, Is.GreaterThan(0));
                Assert.That(tabs.GlobalPosition.Y + tabs.Height,
                    Is.LessThanOrEqualTo(screen.ChatBox.GlobalPosition.Y));
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
