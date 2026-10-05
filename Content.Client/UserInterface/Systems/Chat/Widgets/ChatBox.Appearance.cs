using System.IO;
using System.Linq;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.Chat;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public partial class ChatBox
{
    private Label? _saveStatus;
    private OptionButton? _settingsTabSelect;
    private PanelContainer[] _sidePanels = Array.Empty<PanelContainer>();
    private Texture? _chatImage;
    private string _loadedChatImage = "";
    private string _searchText = "";
    private Label? _searchCount;
    private LineEdit? _fontSizeField;
    private BoxContainer? _settingsFilters;
    private const string LocalImagePath = "/chat_background.txt";

    public void SetSidePanels(params PanelContainer[] panels)
    {
        _sidePanels = panels;
        ApplyPanelAppearance();
    }

    private void ApplyPanelAppearance()
    {
        if (_tabs == null) return;
        var appearance = _tabs.Appearance;
        foreach (var panel in _sidePanels)
            panel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(appearance.PanelColor) };
        if (SettingsPanel.Visible)
            SettingsPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(appearance.PanelColor) };
        var cache = IoCManager.Resolve<IResourceCache>();
        var fontPath = appearance.Font switch
        {
            "NotoSansDisplay" => "/Fonts/NotoSansDisplay/NotoSansDisplay-Regular.ttf",
            "Boxfont" => "/Fonts/Boxfont-round/Boxfont Round.ttf",
            _ => "/Fonts/NotoSans/NotoSans-Regular.ttf"
        };
        var font = cache.GetFont(new[] { fontPath, "/Fonts/NotoSans/NotoSans-Regular.ttf", "/Fonts/NotoEmoji.ttf" }, appearance.FontSize);
        var rules = UserInterfaceManager.Stylesheet?.Rules ?? Array.Empty<StyleRule>();
        Contents.Stylesheet = new Stylesheet(rules.Concat(new StyleRule[] { Element<RichTextLabel>().Prop("font", font) }).ToArray());
        var background = Color.FromHex(appearance.ChatColor);
        if (appearance.Background == "Gradient")
        {
            var bottom = Color.FromHex(appearance.GradientColor);
            background = new Color(MathF.Max(background.R, bottom.R), MathF.Max(background.G, bottom.G), MathF.Max(background.B, bottom.B));
        }
        Contents.MessageBackground = ChatOutputPanel.SurfaceColor(background);
        ChatInput.Input.Stylesheet = new Stylesheet(rules.Concat(new StyleRule[] { Element<LineEdit>().Prop("font", font) }).ToArray());
        ChatInput.Input.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex(appearance.InputColor),
            ContentMarginLeftOverride = 6, ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 5, ContentMarginBottomOverride = 5
        };
        if (_loadedChatImage != appearance.Image)
        {
            if (_chatImage is IDisposable disposable) disposable.Dispose();
            _chatImage = null;
            _loadedChatImage = appearance.Image;
            if (!string.IsNullOrEmpty(appearance.Image))
            {
                try
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(appearance.Image));
                    _chatImage = Texture.LoadFromPNGStream(stream, "Chat background");
                }
                catch (Exception)
                {
                    if (_saveStatus != null) _saveStatus.Text = Loc.GetString("chat-panel-image-invalid");
                }
            }
        }
        ChatWindowPanel.PanelOverride = new ChatBackgroundStyle
        {
            Top = Color.FromHex(appearance.ChatColor), Bottom = Color.FromHex(appearance.GradientColor),
            Gradient = appearance.Background == "Gradient", Image = appearance.Background == "Image" ? _chatImage : null
        };
        ChatInput.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        Repopulate();
    }

    private void OpenPanelSettings()
    {
        if (_tabs == null) return;
        if (SettingsPanel.Visible) { ClosePanelSettings(); return; }
        SettingsPanel.RemoveAllChildren();
        SettingsPanel.Visible = true;
        SettingsPanel.PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex(_tabs.Appearance.PanelColor) };
        LayoutContainer.SetAnchorPreset(SettingsPanel, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorBottom(SettingsPanel, 0.6f);
        LayoutContainer.SetAnchorPreset(Contents, LayoutContainer.LayoutPreset.Wide);
        LayoutContainer.SetAnchorTop(Contents, 0.6f);
        var scroll = new ScrollContainer { HScrollEnabled = false, VScrollEnabled = true, VerticalExpand = true, HorizontalExpand = true };
        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6, HorizontalExpand = true, Margin = new Thickness(8) };
        scroll.AddChild(body);
        SettingsPanel.AddChild(scroll);
        var close = new Button { Text = Loc.GetString("chat-panel-settings-close") };
        close.OnPressed += _ => ClosePanelSettings();
        body.AddChild(close);
        RichTextLabel Caption(string key)
        {
            var caption = new RichTextLabel { HorizontalExpand = true };
            caption.SetMessage(Loc.GetString(key));
            return caption;
        }
        void Heading(string key) => body.AddChild(Caption(key));
        void Row(string key, Control control)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
            row.AddChild(Caption(key));
            row.AddChild(control);
            body.AddChild(row);
        }
        void Changed()
        {
            ApplyPanelAppearance();
            SaveTabs();
        }
        LineEdit TextField(string text, Action<string> update, int max = 512)
        {
            var field = new LineEdit { Text = text, HorizontalExpand = true, IsValid = value => value.Length <= max };
            field.OnTextChanged += _ => { if (!_loadingPanelSettings) update(field.Text); };
            return field;
        }
        void ColorField(string key, string value, Action<string> update)
        {
            var picker = new ChatColorPicker(Color.FromHex(value));
            picker.OnColorChanged += color =>
            {
                update(color.ToHex());
                Changed();
            };
            Row(key, picker);
        }
        Heading("chat-panel-tabs-heading");
        _settingsTabSelect = new OptionButton();
        _settingsTabSelect.OnItemSelected += args => SelectTab(args.Id);
        Row("chat-panel-active-tab", _settingsTabSelect);
        _tabName = TextField(_tabs.Tabs[_tabs.SelectedIndex].Name, text =>
        {
            var name = text.Trim();
            if (name.Length == 0) return;
            _tabs.Tabs[_tabs.SelectedIndex].Name = name;
            RebuildTabButtons();
            RefreshSettingsTabs();
            SaveTabs();
        }, 32);
        Row("chat-panel-tab-name", _tabName);
        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        _addTab = new Button { Text = Loc.GetString("chat-tabs-add") };
        _addTab.OnPressed += _ =>
        {
            if (_tabs.Tabs.Count >= 16) return;
            _tabs.Tabs.Add(new ChatTabSettings { Name = Loc.GetString("chat-tabs-new", ("number", _tabs.Tabs.Count + 1)), Channels = (ChatChannel) uint.MaxValue });
            SelectTab(_tabs.Tabs.Count - 1);
        };
        _removeTab = new Button { Text = Loc.GetString("chat-tabs-remove") };
        _removeTab.OnPressed += _ =>
        {
            if (_tabs.Tabs.Count <= 1) return;
            _tabs.Tabs.RemoveAt(_tabs.SelectedIndex);
            SelectTab(Math.Min(_tabs.SelectedIndex, _tabs.Tabs.Count - 1));
        };
        buttons.AddChild(_addTab);
        buttons.AddChild(_removeTab);
        body.AddChild(buttons);
        Heading("chat-tabs-filters");
        _settingsFilters = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        body.AddChild(_settingsFilters);
        RefreshSettingsFilters();
        Heading("chat-panel-appearance-heading");
        var a = _tabs.Appearance;
        ColorField("chat-panel-color", a.PanelColor, value => a.PanelColor = value);
        ColorField("chat-panel-chat-color", a.ChatColor, value => a.ChatColor = value);
        ColorField("chat-panel-gradient-color", a.GradientColor, value => a.GradientColor = value);
        ColorField("chat-panel-input-color", a.InputColor, value => a.InputColor = value);
        var background = new OptionButton();
        var modes = new[] { "Solid", "Gradient", "Image" };
        for (var i = 0; i < modes.Length; i++) background.AddItem(Loc.GetString("chat-panel-background-" + modes[i].ToLowerInvariant()), i);
        background.SelectId(Array.IndexOf(modes, a.Background));
        background.OnItemSelected += args => { background.SelectId(args.Id); a.Background = modes[args.Id]; Changed(); };
        Row("chat-panel-background", background);
        var image = new Button { Text = Loc.GetString("chat-panel-image-choose") };
        image.OnPressed += async _ =>
        {
            try
            {
                var dialogs = IoCManager.Resolve<IFileDialogManager>();
                await using var file = await dialogs.OpenFile(new FileDialogFilters(new FileDialogFilters.Group("png")));
                if (file == null || !SettingsPanel.Visible) return;
                using var memory = new MemoryStream();
                await file.CopyToAsync(memory);
                if (memory.Length > 262144) { if (_saveStatus != null) _saveStatus.Text = Loc.GetString("chat-panel-image-limit"); return; }
                using var validation = new MemoryStream(memory.ToArray());
                var texture = Texture.LoadFromPNGStream(validation, "Validate chat background");
                if (texture is IDisposable disposable) disposable.Dispose();
                if (!SettingsPanel.Visible) return;
                a.Image = Convert.ToBase64String(memory.ToArray());
                StoreLocalChatImage(a.Image);
                a.Background = "Image";
                background.SelectId(2);
                Changed();
            }
            catch (Exception) { if (_saveStatus != null) _saveStatus.Text = Loc.GetString("chat-panel-image-invalid"); }
        };
        body.AddChild(image);
        var clearImage = new Button { Text = Loc.GetString("chat-panel-image-clear") };
        clearImage.OnPressed += _ => { StoreLocalChatImage(""); a.Image = ""; a.Background = "Solid"; background.SelectId(0); Changed(); };
        body.AddChild(clearImage);
        body.AddChild(Caption("chat-panel-image-local"));
        var fonts = new[] { "NotoSans", "NotoSansDisplay", "Boxfont" };
        var font = new OptionButton();
        for (var i = 0; i < fonts.Length; i++) font.AddItem(fonts[i], i);
        font.SelectId(Array.IndexOf(fonts, a.Font));
        font.OnItemSelected += args => { font.SelectId(args.Id); a.Font = fonts[args.Id]; Changed(); };
        Row("chat-panel-font", font);
        _fontSizeField = TextField(a.FontSize.ToString(), text => { if (int.TryParse(text, out var size)) { a.FontSize = Math.Clamp(size, 8, 36); Changed(); } }, 2);
        Row("chat-panel-font-size", _fontSizeField);
        Row("chat-panel-highlight-words", TextField(a.HighlightWords, text => { a.HighlightWords = text; Changed(); }));
        var whole = new CheckBox { Text = Loc.GetString("chat-panel-whole-words"), Pressed = a.WholeWords };
        whole.OnToggled += args => { a.WholeWords = args.Pressed; Changed(); };
        body.AddChild(whole);
        ColorField("chat-panel-highlight-color", a.HighlightColor, value => a.HighlightColor = value);
        body.AddChild(Caption("chat-panel-shortcuts"));
        _saveStatus = new Label { Text = _settingsSaveDelay >= 0 ? Loc.GetString("chat-panel-saving") : "", ClipText = true };
        body.AddChild(_saveStatus);
        RefreshSettingsTabs();
        RebuildTabButtons();
    }

    private void ClosePanelSettings()
    {
        if (_settingsSaveDelay >= 0) FlushPanelSettings();
        SettingsPanel.Visible = false;
        SettingsPanel.RemoveAllChildren();
        LayoutContainer.SetAnchorTop(Contents, 0);
        _tabName = null; _settingsTabSelect = null; _fontSizeField = null; _saveStatus = null; _settingsFilters = null;
    }

    private void RefreshSettingsFilters()
    {
        if (_settingsFilters == null || _tabs == null) return;
        _settingsFilters.RemoveAllChildren();
        foreach (var channel in Enum.GetValues<ChatChannel>())
        {
            if (channel == 0 || (_controller.FilterableChannels & channel) == 0) continue;
            var checkbox = new ChannelFilterCheckbox(channel) { Pressed = (_tabs.Tabs[_tabs.SelectedIndex].Channels & channel) != 0 };
            checkbox.OnToggled += args =>
            {
                var tab = _tabs.Tabs[_tabs.SelectedIndex];
                if (args.Pressed) tab.Channels |= channel; else tab.Channels &= ~channel;
                ChatInput.FilterButton.Popup.SetSelectedChannels(tab.Channels);
                OnChannelFilter(channel, args.Pressed);
            };
            _settingsFilters.AddChild(checkbox);
        }
    }

    private void StoreLocalChatImage(string image)
    {
        using var writer = IoCManager.Resolve<IResourceManager>().UserData.OpenWriteText(new ResPath(LocalImagePath));
        writer.Write(image);
    }

    private void RestoreLocalChatImage()
    {
        if (_tabs == null) return;
        var resources = IoCManager.Resolve<IResourceManager>();
        if (resources.UserData.TryReadAllText(new ResPath(LocalImagePath), out var local))
            _tabs.Appearance.Image = local;
        else if (!string.IsNullOrEmpty(_tabs.Appearance.Image))
            StoreLocalChatImage(_tabs.Appearance.Image);
    }

    private void RefreshSettingsTabs()
    {
        if (_tabs == null || _settingsTabSelect == null) return;
        _settingsTabSelect.Clear();
        for (var i = 0; i < _tabs.Tabs.Count; i++) _settingsTabSelect.AddItem(_tabs.Tabs[i].Name, i);
        _settingsTabSelect.SelectId(_tabs.SelectedIndex);
    }

    private void InitializePanelShortcuts()
    {
        _searchCount = SearchCount;
        SearchInput.PlaceHolder = Loc.GetString("chat-panel-search");
        SearchInput.OnTextChanged += _ => { _searchText = SearchInput.Text.Trim(); Repopulate(); };
        CloseSearch.OnPressed += _ => CloseChatSearch();
        SearchInput.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.TextReleaseFocus) { CloseChatSearch(); args.Handle(); }
        };
        Contents.CanKeyboardFocus = true;
        Contents.OnKeyBindDown += args => { if (args.Function == EngineKeyFunctions.UIClick) Contents.GrabKeyboardFocus(); };
        OnKeyBindDown += HandlePanelShortcut;
        Contents.OnKeyBindDown += HandlePanelShortcut;
        ChatInput.Input.OnKeyBindDown += HandlePanelShortcut;
        SearchInput.OnKeyBindDown += HandlePanelShortcut;
    }

    private void HandlePanelShortcut(GUIBoundKeyEventArgs args)
    {
        if (args.Handled) return;
        if (args.Function == ContentKeyFunctions.ChatSearch) { OpenSearch(); args.Handle(); }
        if (args.Function == ContentKeyFunctions.ChatZoomIn) { AdjustChatFont(1); args.Handle(); }
        if (args.Function == ContentKeyFunctions.ChatZoomOut) { AdjustChatFont(-1); args.Handle(); }
    }

    private void CloseChatSearch()
    {
        SearchBar.Visible = false;
        SearchInput.Text = "";
        _searchText = "";
        Repopulate();
        Contents.GrabKeyboardFocus();
    }

    public void OpenSearch()
    {
        if (_tabs == null) return;
        SearchBar.Visible = true;
        SearchInput.GrabKeyboardFocus();
    }

    public void AdjustChatFont(int change)
    {
        if (_tabs == null) return;
        _tabs.Appearance.FontSize = Math.Clamp(_tabs.Appearance.FontSize + change, 8, 36);
        if (_fontSizeField != null) _fontSizeField.Text = _tabs.Appearance.FontSize.ToString();
        ApplyPanelAppearance();
        SaveTabs();
    }
}
