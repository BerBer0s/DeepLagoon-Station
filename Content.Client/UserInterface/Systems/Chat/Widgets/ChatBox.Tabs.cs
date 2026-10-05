using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Robust.Client.UserInterface.Controls;
using Content.Client.Lobby;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private ChatTabsSettings? _tabs;
    private BoxContainer? _tabButtons;
    private LineEdit? _tabName;
    private Button? _addTab;
    private Button? _removeTab;
    private IClientPreferencesManager? _panelPreferences;
    private float _settingsSaveDelay = -1;
    private bool _loadingPanelSettings;

    public void EnableTabs(PanelContainer panel)
    {
        if (_tabs != null)
            return;
        _panelPreferences = IoCManager.Resolve<IClientPreferencesManager>();
        _tabs = ChatTabsSettings.Deserialize(_panelPreferences.Preferences?.ChatPanelSettings ?? "") ??
            ChatTabsSettings.Deserialize(_cfg.GetCVar(CCVars.ChatTabs)) ?? new ChatTabsSettings
        {
            Tabs = new()
            {
                new() { Name = Loc.GetString("chat-tabs-all"), Channels = (ChatChannel) uint.MaxValue },
                new() { Name = Loc.GetString("chat-tabs-local"), Channels = ChatChannel.Local | ChatChannel.Whisper | ChatChannel.Emotes | ChatChannel.Radio | ChatChannel.Notifications | ChatChannel.Server },
                new() { Name = "OOC", Channels = ChatChannel.OOC | ChatChannel.LOOC | ChatChannel.Server }
            }
        };
        RestoreLocalChatImage();
        LayoutContainer.SetAnchorPreset(Contents, LayoutContainer.LayoutPreset.Wide);

        var toolbar = new BoxContainer { SeparationOverride = 4 };
        _tabButtons = new BoxContainer { SeparationOverride = 2 };
        var scroll = new ScrollContainer { HScrollEnabled = true, VScrollEnabled = false, HorizontalExpand = true, ReturnMeasure = true };
        scroll.AddChild(_tabButtons);
        toolbar.AddChild(scroll);

        var settings = new Button { Text = Loc.GetString("chat-panel-settings") };
        settings.OnPressed += _ => OpenPanelSettings();
        toolbar.AddChild(settings);
        ChatInput.FilterButton.Visible = false;
        panel.AddChild(toolbar);
        _panelPreferences.OnServerDataLoaded += LoadServerPanelSettings;
        _panelPreferences.OnChatPanelSaved += OnPanelSettingsSaved;
        InitializePanelShortcuts();
        _controller.FilterableChannelsChanged += OnTabChannelsChanged;
        SelectTab(_tabs.SelectedIndex);
        ApplyPanelAppearance();
    }

    private void SelectTab(int index)
    {
        if (_tabs == null)
            return;
        ChatInput.FilterButton.Popup.Close();
        _tabs.SelectedIndex = index;
        ChatInput.FilterButton.Popup.SetSelectedChannels(_tabs.Tabs[index].Channels);
        if (_tabName != null)
        {
            var wasLoading = _loadingPanelSettings;
            _loadingPanelSettings = true;
            _tabName.Text = _tabs.Tabs[index].Name;
            _loadingPanelSettings = wasLoading;
        }
        RebuildTabButtons();
        RefreshSettingsTabs();
        RefreshSettingsFilters();
        Repopulate();
        _controller.ClearUnfilteredUnreads(ChatInput.FilterButton.Popup.GetActive());
        SaveTabs();
    }

    private void RebuildTabButtons()
    {
        if (_tabs == null || _tabButtons == null)
            return;
        _tabButtons.RemoveAllChildren();
        var group = new ButtonGroup(false);
        for (var i = 0; i < _tabs.Tabs.Count; i++)
        {
            var index = i;
            var button = new Button { Text = _tabs.Tabs[i].Name, ToggleMode = true, Group = group };
            button.OnPressed += _ => SelectTab(index);
            _tabButtons.AddChild(button);
        }
        // A non-empty group automatically selects its first button. Select the
        // requested button through the group instead of explicitly unsetting it.
        group.Buttons[_tabs.SelectedIndex].Pressed = true;
        if (_addTab != null) _addTab.Disabled = _tabs.Tabs.Count >= 16;
        if (_removeTab != null) _removeTab.Disabled = _tabs.Tabs.Count <= 1;
    }

    private void SaveActiveTabFilters()
    {
        if (_tabs == null)
            return;
        _tabs.Tabs[_tabs.SelectedIndex].Channels = ChatInput.FilterButton.Popup.SelectedChannels;
        SaveTabs();
    }

    private void SaveTabs()
    {
        if (_tabs == null)
            return;
        if (_loadingPanelSettings) return;
        _settingsSaveDelay = 0.4f;
        if (_saveStatus != null) _saveStatus.Text = Loc.GetString("chat-panel-saving");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_settingsSaveDelay < 0) return;
        _settingsSaveDelay -= args.DeltaSeconds;
        if (_settingsSaveDelay <= 0) FlushPanelSettings();
    }

    private void FlushPanelSettings()
    {
        _settingsSaveDelay = -1;
        if (_tabs != null && _panelPreferences?.ServerDataLoaded == true)
            _panelPreferences.UpdateChatPanelSettings(_tabs.Serialize());
    }

    private void LoadServerPanelSettings()
    {
        if (_tabs == null || _panelPreferences == null) return;
        var saved = ChatTabsSettings.Deserialize(_panelPreferences.Preferences?.ChatPanelSettings ?? "");
        if (saved == null) { SaveTabs(); return; }
        _loadingPanelSettings = true;
        _tabs = saved;
        var hadServerImage = !string.IsNullOrEmpty(saved.Appearance.Image);
        RestoreLocalChatImage();
        SelectTab(_tabs.SelectedIndex);
        ApplyPanelAppearance();
        _loadingPanelSettings = false;
        if (hadServerImage) SaveTabs();
    }

    private void OnPanelSettingsSaved(bool success)
    {
        if (_saveStatus != null) _saveStatus.Text = Loc.GetString(success ? "chat-panel-saved" : "chat-panel-save-failed");
    }

    private void OnTabChannelsChanged(ChatChannel channels)
    {
        if (_tabs != null)
            Repopulate();
        RefreshSettingsFilters();
    }
}
