using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using NUnit.Framework;

namespace Content.Tests.Client;

[TestFixture]
public sealed class ChatTabsSettingsTest
{
    [Test]
    public void WebSettingsSurviveWithoutReplacingNativeTabs()
    {
        var settings = new ChatTabsSettings
        {
            Tabs = new() { new() { Name = "Local", Channels = ChatChannel.Local } },
            WebState = "{\"v\":1,\"settings\":{\"theme\":\"dark\"}}"
        };
        settings.PinnedEmotes.AddRange(new[] { "Smile", "Wave" });
        var restored = ChatTabsSettings.Deserialize(settings.Serialize())!;
        Assert.That(restored.WebState, Is.EqualTo(settings.WebState));
        Assert.That(restored.Tabs[0].Channels, Is.EqualTo(ChatChannel.Local));
        Assert.That(restored.PinnedEmotes, Is.EqualTo(new[] { "Smile", "Wave" }));
    }
    [Test]
    public void FiltersAndSelectedTabSurviveSaving()
    {
        var settings = new ChatTabsSettings
        {
            SelectedIndex = 1,
            Tabs = new()
            {
                new() { Name = "Local", Channels = ChatChannel.Local | ChatChannel.Emotes },
                new() { Name = "Ghost/Admin", Channels = ChatChannel.Dead | ChatChannel.Admin }
            }
        };
        var restored = ChatTabsSettings.Deserialize(settings.Serialize())!;
        Assert.That(restored.SelectedIndex, Is.EqualTo(1));
        Assert.That(restored.Tabs[1].Name, Is.EqualTo("Ghost/Admin"));
        Assert.That(restored.Tabs[1].Channels, Is.EqualTo(ChatChannel.Dead | ChatChannel.Admin));
        restored.Tabs[1].Channels &= ~ChatChannel.Admin;
        Assert.That(restored.Tabs[0].Channels, Is.EqualTo(ChatChannel.Local | ChatChannel.Emotes));
        Assert.That(settings.Tabs[1].Channels, Is.EqualTo(ChatChannel.Dead | ChatChannel.Admin));
    }

    [TestCase("")]
    [TestCase("invalid JSON")]
    [TestCase("null")]
    [TestCase("{\"Tabs\":null}")]
    [TestCase("{\"Tabs\":[]}")]
    [TestCase("{\"Tabs\":[null]}")]
    [TestCase("{\"Tabs\":[{\"Name\":\" \"}]}")]
    public void InvalidSavedTabsFallBackToDefaults(string json)
    {
        Assert.That(ChatTabsSettings.Deserialize(json), Is.Null);
    }

    [Test]
    public void PreviousJsonSettingsAreStillReadable()
    {
        const string json = "{\"Tabs\":[{\"Name\":\"Все: чат\",\"Channels\":4294967295}],\"SelectedIndex\":0}";
        var settings = ChatTabsSettings.Deserialize(json)!;
        Assert.That(settings.Tabs[0].Name, Is.EqualTo("Все: чат"));
        Assert.That(settings.Tabs[0].Channels, Is.EqualTo((ChatChannel) uint.MaxValue));
        Assert.That(ChatTabsSettings.Deserialize(settings.Serialize())!.Tabs[0].Name, Is.EqualTo("Все: чат"));
    }

    [Test]
    public void RemovedSelectedTabDoesNotLeaveInvalidIndex()
    {
        var settings = new ChatTabsSettings
        {
            SelectedIndex = 5,
            Tabs = new() { new() { Name = "Local", Channels = ChatChannel.Local } }
        };
        Assert.That(ChatTabsSettings.Deserialize(settings.Serialize())!.SelectedIndex, Is.Zero);
    }
}
