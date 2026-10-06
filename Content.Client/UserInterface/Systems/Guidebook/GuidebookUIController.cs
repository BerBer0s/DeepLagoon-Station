using Content.Client._DeepLagoon.WebUI;
using Content.Client.Gameplay;
using Content.Client.Guidebook;
using Content.Client.Lobby;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.UserInterface.Controls;
using Content.Shared.Guidebook;
using Content.Shared.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Systems.Guidebook;

public sealed partial class GuidebookUIController : UIController, IOnStateEntered<LobbyState>, IOnStateEntered<GameplayState>, IOnStateExited<LobbyState>, IOnStateExited<GameplayState>, IOnSystemChanged<GuidebookSystem>
{
    [UISystemDependency] private readonly GuidebookSystem _guidebookSystem = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirements = default!;
    private WikiGuidebookWindow? _guideWindow;
    private MenuButton? GuidebookButton => UIManager.GetActiveUIWidgetOrNull<MenuBar.Widgets.GameTopMenuBar>()?.GuidebookButton;

    public void OnStateEntered(LobbyState state) => Enter(state is LobbyState);
    public void OnStateEntered(GameplayState state) => Enter(false);
    private void Enter(bool lobby)
    {
        _guideWindow = UIManager.CreateWindow<WikiGuidebookWindow>();
        _guideWindow.OnOpen += OnWindowOpen;
        _guideWindow.OnClose += OnWindowClosed;
        CommandBinds.Builder.Bind(ContentKeyFunctions.OpenGuidebook,
            InputCmdHandler.FromDelegate(_ => ToggleGuidebook())).Register<GuidebookUIController>();
        if (lobby && _jobRequirements.FetchOverallPlaytime() < TimeSpan.FromMinutes(180))
            OpenGuidebook();
    }
    public void OnStateExited(LobbyState state) => Exit();
    public void OnStateExited(GameplayState state) => Exit();
    private void Exit()
    {
        if (_guideWindow != null)
        {
            _guideWindow.OnOpen -= OnWindowOpen;
            _guideWindow.OnClose -= OnWindowClosed;
            _guideWindow.Dispose();
            _guideWindow = null;
        }
        CommandBinds.Unregister<GuidebookUIController>();
    }
    public void OnSystemLoaded(GuidebookSystem system) => system.OnGuidebookOpen += OpenGuidebook;
    public void OnSystemUnloaded(GuidebookSystem system) => system.OnGuidebookOpen -= OpenGuidebook;
    internal void UnloadButton()
    {
        if (GuidebookButton != null) GuidebookButton.OnPressed -= GuidebookButtonOnPressed;
    }
    internal void LoadButton()
    {
        if (GuidebookButton != null) GuidebookButton.OnPressed += GuidebookButtonOnPressed;
    }
    private void GuidebookButtonOnPressed(ButtonEventArgs args) => ToggleGuidebook();
    private void OnWindowOpen()
    {
        if (GuidebookButton != null) GuidebookButton.Pressed = true;
    }
    private void OnWindowClosed()
    {
        if (GuidebookButton != null) GuidebookButton.Pressed = false;
    }
    public void ToggleGuidebook()
    {
        if (_guideWindow?.IsOpen == true) CloseGuidebook();
        else OpenGuidebook();
    }
    // Keep the existing API used by help verbs, rules, and the guidebook keybinding.
    // Documents are now resolved exclusively through explicit wikiPage mappings.
    public void OpenGuidebook(
        Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>? guides = null,
        List<ProtoId<GuideEntryPrototype>>? rootEntries = null,
        ProtoId<GuideEntryPrototype>? forceRoot = null,
        bool includeChildren = true,
        ProtoId<GuideEntryPrototype>? selected = null)
    {
        if (_guideWindow == null) return;
        var guide = selected ?? forceRoot;
        if (guide == null && guides?.Count == 1)
            foreach (var id in guides.Keys) guide = id;
        _guideWindow.ShowGuide(guide?.ToString());
        _guideWindow.OpenCenteredRight();
    }
    public void OpenGuidebook(
        List<ProtoId<GuideEntryPrototype>> guideList,
        List<ProtoId<GuideEntryPrototype>>? rootEntries = null,
        ProtoId<GuideEntryPrototype>? forceRoot = null,
        bool includeChildren = true,
        ProtoId<GuideEntryPrototype>? selected = null)
    {
        OpenGuidebook(selected: selected ?? forceRoot ?? (guideList.Count == 1 ? guideList[0] : (ProtoId<GuideEntryPrototype>?) null));
    }
    public void OpenWikiPage(string pageId)
    {
        if (_guideWindow == null) return;
        _guideWindow.ShowPage(pageId);
        _guideWindow.OpenCenteredRight();
    }
    public void CloseGuidebook() => _guideWindow?.Close();
}
