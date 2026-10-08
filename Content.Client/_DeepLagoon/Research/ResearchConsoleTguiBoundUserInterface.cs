using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared.Access.Systems;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.Research;

/// <summary>
/// TGUI replacement for the native R&amp;D console window. It reuses the existing console state and
/// messages, so the server and the network protocol are unchanged.
/// </summary>
[UsedImplicitly]
public sealed class ResearchConsoleTguiBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private const string InterfaceName = "ResearchConsole";
    private const int MaxIconRequest = 128;

    // The embedded browser is closed with the window, but CEF keeps querying the control's view rect
    // for a moment afterwards; the engine answers a disposed control with an empty rect, which crashes
    // libcef. Opening and closing quickly hits that race, so the window object outlives its browser.
    // TEMPORARY: remove together with the delayed Dispose once the engine's GetViewRect is fixed.
    private const int WindowDisposeDelayMs = 3000;

    private ResearchConsoleTguiWindow? _window;
    private ResearchConsoleTguiData? _data;
    private string _title = string.Empty;
    private string _staticJson = string.Empty;
    private string? _staticKey;
    private HashSet<string> _knownTechnologies = new();

    protected override void Open()
    {
        base.Open();

        _data = new ResearchConsoleTguiData(EntMan);
        _title = Loc.GetString("research-console-menu-title");

        _window = new ResearchConsoleTguiWindow { Title = _title };
        _window.OnClose += Close;
        var userInterface = EntMan.System<UserInterfaceSystem>();
        userInterface.RegisterControl(this, _window);
        if (userInterface.TryGetPosition(Owner, UiKey, out var position))
            _window.Open(position);
        else
            _window.OpenCentered();
        _window.Panel.OnClose += Close;
        _window.Panel.OnAction += OnAction;
        // The panel publishes its own state first on every (re)load; static data follows it.
        _window.Panel.Web.Ready += SendStatic;
        _window.Panel.SetState(InterfaceName, "{}", _title);
        PushState();
    }

    public override void OnProtoReload(PrototypesReloadedEventArgs args)
    {
        base.OnProtoReload(args);

        if (!args.WasModified<TechnologyPrototype>())
            return;

        ResearchConsoleTguiData.ClearCache();
        _staticKey = null;
        PushState();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        PushState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window is { } window)
        {
            _window = null;
            window.OnClose -= Close;
            window.Close();
            Timer.Spawn(WindowDisposeDelayMs, () =>
            {
                // Disposing releases the browser's text input; BUI-owned windows are disposed the same way.
#pragma warning disable CS0618
                if (!window.Disposed)
                    window.Dispose();
#pragma warning restore CS0618
            });
        }

        base.Dispose(disposing);
    }

    private void PushState()
    {
        if (_window == null || _data == null || State is not ResearchConsoleBoundInterfaceState state)
            return;

        var staticKey = string.Join(',', state.Researches.Keys.OrderBy(id => id, StringComparer.Ordinal));
        if (staticKey != _staticKey)
        {
            _staticKey = staticKey;
            _knownTechnologies = new HashSet<string>(state.Researches.Keys);
            _staticJson = _data.BuildStatic(_knownTechnologies, staticKey);
            SendStatic();
        }

        _window.Panel.SetState(InterfaceName, _data.BuildDynamic(state, HasAccess()), _title);
    }

    private void SendStatic()
    {
        if (_window is { Panel.Web.IsReady: true } && _staticJson.Length > 0)
            _window.Panel.Web.Send("update", _staticJson);
    }

    private bool HasAccess()
    {
        var player = IoCManager.Resolve<IPlayerManager>();
        return player.LocalEntity is { } entity && EntMan.System<AccessReaderSystem>().IsAllowed(entity, Owner);
    }

    private void OnAction(string action, string payload)
    {
        switch (action)
        {
            case "research":
                if (TguiActionData.TryParse(payload, out var data) &&
                    data?.String("id") is { } id &&
                    _knownTechnologies.Contains(id))
                {
                    SendMessage(new ConsoleUnlockTechnologyMessage(id));
                }
                break;
            case "servers":
                SendMessage(new ConsoleServerSelectionMessage());
                break;
            case "icons":
                SendIcons(payload);
                break;
        }
    }

    private void SendIcons(string payload)
    {
        if (_window == null || _data == null ||
            !TguiActionData.TryParse(payload, out var data) || data?.String("ids") is not { } ids)
        {
            return;
        }

        var requested = ids.Split(',')
            .Where(_knownTechnologies.Contains)
            .Distinct()
            .Take(MaxIconRequest)
            .ToList();
        if (requested.Count > 0)
            _window.Panel.Web.Send("update", _data.BuildIcons(requested));
    }
}
