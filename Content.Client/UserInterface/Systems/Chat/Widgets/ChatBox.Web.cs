using Content.Client._DeepLagoon.WebUI;
using Content.Shared.Chat;
using Content.Client.Lobby;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.UserInterface.Controls;
using System.Linq;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private GameWebView? _webChat;
    private bool _repopulatingWeb;
    private IClientPreferencesManager? _webPreferences;
    private float _emoteRefresh;
    private string _lastEmoteState = "";
    private string? _lastWebHistoryPayload;
    public GameWebView? WebChat => _webChat;

    public void EnableTguiChat()
    {
        _webChatRequested = true;
        if (_useVanillaChat)
        {
            ChatInput.UseVanillaLayout();
            return;
        }
        ChatInput.Visible = false;
        if (_webChat != null)
            return;
        _webChat = new GameWebView(chat: true, suspendWhenHidden: true)
        {
            Name = "TguiChat",
            MinHeight = 120
        };
        _webPreferences = IoCManager.Resolve<IClientPreferencesManager>();
        _webPreferences.OnServerDataLoaded += RestoreWebSettings;
        _webChat.Message += (type, payload) =>
        {
            if (type == "act/compose") { Focus(); return; }
            if (type is "act/emote" or "act/emote-pin")
            {
                HandleEmoteShortcut(type, payload);
                return;
            }
            if (type != "panel/state_set" || payload.Length > 8192 || _webPreferences.Preferences == null)
                return;
            var settings = ChatTabsSettings.Deserialize(_webPreferences.Preferences.ChatPanelSettings) ?? new();
            if (settings.Tabs.Count == 0)
                settings.Tabs.Add(new ChatTabSettings { Name = Loc.GetString("chat-tabs-all"), Channels = (ChatChannel) uint.MaxValue });
            // Keep native tabs/settings alongside the original TGUI JSON.
            settings.WebState = payload;
            _webPreferences.UpdateChatPanelSettings(settings.Serialize());
        };
        LayoutContainer.SetAnchorPreset(_webChat, LayoutContainer.LayoutPreset.Wide);
        ChatBody.AddChild(_webChat);
        Contents.Visible = false;
        // The controller owns history. Do not keep a second, hidden UI history.
        Contents.Clear();
        _webChat.Ready += () =>
        {
            RestoreWebSettings();
            _lastEmoteState = "";
            PublishEmoteShortcuts();
            PublishWebHistory(force: true);
        };
    }

    private void RestoreWebSettings()
    {
        if (_webChat?.IsReady != true || _webPreferences?.Preferences == null) return;
        var settings = ChatTabsSettings.Deserialize(_webPreferences.Preferences.ChatPanelSettings);
        if (!string.IsNullOrEmpty(settings?.WebState))
            _webChat.Send("panel/state", "{\"state\":" + GameWebView.Quote(settings.WebState) + "}");
        _lastEmoteState = "";
        PublishEmoteShortcuts();
    }

    private void UpdateWebEmotes(FrameEventArgs args)
    {
        if (_useVanillaChat || _webChat?.IsReady != true) return;
        _emoteRefresh -= args.DeltaSeconds;
        if (_emoteRefresh > 0) return;
        _emoteRefresh = 0.75f;
        PublishEmoteShortcuts();
    }

    private ChatTabsSettings CurrentWebSettings()
    {
        var settings = ChatTabsSettings.Deserialize(_webPreferences?.Preferences?.ChatPanelSettings ?? "") ?? new();
        if (settings.Tabs.Count == 0)
            settings.Tabs.Add(new ChatTabSettings { Name = Loc.GetString("chat-tabs-all"), Channels = (ChatChannel) uint.MaxValue });
        return settings;
    }

    private void HandleEmoteShortcut(string type, string payload)
    {
        if (!TguiActionData.TryParse(payload, out var args) || args!.String("id") is not { Length: > 0 and <= 64 } id) return;
        var emotes = _entManager.System<EmoteShortcutSystem>();
        if (type == "act/emote") { emotes.TryPlay(id); return; }
        if (_webPreferences?.ServerDataLoaded != true) return;
        var settings = CurrentWebSettings();
        if (!settings.PinnedEmotes.Remove(id))
        {
            if (settings.PinnedEmotes.Count >= 24 || !emotes.Available().Any(emote => emote.ID == id)) return;
            settings.PinnedEmotes.Add(id);
        }
        _webPreferences.UpdateChatPanelSettings(settings.Serialize());
        PublishEmoteShortcuts();
    }

    private void PublishEmoteShortcuts()
    {
        if (_webChat?.IsReady != true) return;
        var available = _entManager.System<EmoteShortcutSystem>().Available();
        var settings = CurrentWebSettings();
        var json = "{\"available\":[" + string.Join(',', available.Select(emote =>
            "{\"id\":" + GameWebView.Quote(emote.ID) + ",\"name\":" + GameWebView.Quote(Loc.GetString(emote.Name)) + "}")) +
            "],\"pinned\":[" + string.Join(',', settings.PinnedEmotes.Select(GameWebView.Quote)) + "]}";
        if (json == _lastEmoteState) return;
        _lastEmoteState = json;
        _webChat.Send("deeplagoon/emotes", json);
    }

    private void DisposeWebSettings()
    {
        if (_webPreferences != null)
            _webPreferences.OnServerDataLoaded -= RestoreWebSettings;
    }

    private void PushWebMessage(ChatMessage message)
    {
        if (_useVanillaChat || _webChat?.IsReady != true || _repopulatingWeb || message.HideChat)
            return;
        _webChat.Send("chat/message", WebChatMessageFormatter.BuildPayload(message));
        _lastWebHistoryPayload = null;
    }

    private void PublishWebHistory(bool force = false)
    {
        if (_useVanillaChat || _webChat?.IsReady != true) return;
        var payload = "[" + string.Join(',', _controller.History.Where(entry => !entry.Msg.HideChat)
            .Select(entry => WebChatMessageFormatter.BuildPayload(entry.Msg))) + "]";
        if (!force && payload == _lastWebHistoryPayload) return;
        _lastWebHistoryPayload = payload;
        // Replace messages in the existing document. Reloading it flashes the
        // lobby panel, restarts CEF work and loses open menus/drafts.
        _webChat.Send("chat/replace", payload);
    }
}
