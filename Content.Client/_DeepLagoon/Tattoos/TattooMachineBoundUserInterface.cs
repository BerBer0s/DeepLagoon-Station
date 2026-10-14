using System.Numerics;
using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._DeepLagoon.Tattoos;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using JetBrains.Annotations;
using Robust.Shared.Configuration;

namespace Content.Client._DeepLagoon.Tattoos;

[UsedImplicitly]
public sealed class TattooMachineBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private WebUiWindow? _window;
    private SpriteView? _preview;
    private readonly TattooEditorData _data = new();
    private TattooMachineState? _state;
    private string _status = "";
    private bool _pending;
    private bool _fileBusy;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<WebUiWindow>();
        _window.Title = "Тату-машинка";
        _window.SetSize = new Vector2(930, 640);
        _window.MinSize = new Vector2(730, 520);
        _window.Panel.Orphan();
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true };
        row.AddChild(_window.Panel);
        _preview = new SpriteView { SetSize = new Vector2(180, 360), VerticalAlignment = Control.VAlignment.Center,
            Scale = new Vector2(6, 6), OverrideDirection = Direction.South };
        row.AddChild(_preview);
        _window.ContentsContainer.AddChild(row);
        _window.Panel.OnClose += Close;
        _window.Panel.OnAction += OnAction;
        Push();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not TattooMachineState tattooState) return;
        _state = tattooState;
        _status = tattooState.Status;
        _pending = false;
        Push();
    }

    private void Push()
    {
        if (_window == null || _state == null) return;
        var target = EntMan.GetEntity(_state.Target);
        if (!EntMan.EntityExists(target)) return;
        _preview?.SetEntity(target);
        _window.Panel.SetState("TattooStudio", _data.Build(target, _state.Tattoos, _status, true).Bool("pending", _pending).Bool("fileBusy", _fileBusy).ToString(), "Тату-машинка");
    }

    private void OnAction(string action, string payload)
    {
        if (action == "close") { Close(); return; }
        if (_state == null || !TguiActionData.TryParse(payload, out var args)) return;
        if (action is "tattoo-export" or "tattoo-import")
        {
            if (_fileBusy || _pending || _state.Tattoos.FirstOrDefault(t => t.Id == args!.String("id")) is not { } tattoo) return;
            if (action == "tattoo-import" && !IoCManager.Resolve<IConfigurationManager>().GetCVar(TattooCVars.Enabled)) return;
            HandleFileAction(action, tattoo.Clone()); return;
        }
        if (_fileBusy) return;
        if (!IoCManager.Resolve<IConfigurationManager>().GetCVar(TattooCVars.Enabled)) return;
        if (action == "tattoo-bake-chunk")
        {
            if (_pending || !_data.ReceiveBakeChunk(args!, out var completed)) return;
            if (completed == null) return;
            args = completed; action = "tattoo-bake";
        }
        if (action == "tattoo-preview-direction" && args!.TryInt("direction", out var direction) && direction is >= 0 and < 4)
        {
            if (_preview != null) _preview.OverrideDirection = direction switch { 1 => Direction.North, 2 => Direction.East, 3 => Direction.West, _ => Direction.South };
            return;
        }
        if (_pending) return;
        if (action == "tattoo-bake") { HandleBake(args!); return; }
        if (!TattooEditorData.TryApply(action, args!, _state.Tattoos, out var next, out _status)) { Push(); return; }
        _pending = true;
        SendMessage(new TattooMachineApplyMessage(next, _state.Revision));
        Push();
    }

    private async void HandleBake(TguiActionData args)
    {
        if (_window == null || _state == null || args.String("recipe") is not { } recipe
            || !TattooEditorData.TryApply("tattoo-transform", args, _state.Tattoos, out var next, out _status)
            || next.FirstOrDefault(t => t.Id == args.String("id")) is not { } basis) return;
        var window = _window; var target = _state.Target; var revision = _state.Revision;
        _fileBusy = true; _status = "Сервер подтверждает рисунок…"; Push();
        try
        {
            var result = await EntMan.System<TattooArtworkSystem>().Authorize(basis, recipe);
            if (_window != window || !window.IsOpen || _state?.Target != target || _state.Revision != revision
                || !IoCManager.Resolve<IConfigurationManager>().GetCVar(TattooCVars.Enabled)) return;
            next[next.FindIndex(t => t.Id == basis.Id)] = result;
            _pending = true; _status = ""; SendMessage(new TattooMachineApplyMessage(next, revision));
        }
        catch (Exception e) { _status = e.Message; }
        finally { _fileBusy = false; if (_window == window && window.IsOpen) Push(); }
    }

    private async void HandleFileAction(string action, Tattoo template)
    {
        if (_window == null || _state == null) return;
        var window = _window; var target = _state.Target; var revision = _state.Revision;
        _fileBusy = true; _status = "Выбор PNG…"; Push();
        try
        {
            if (action == "tattoo-export") _status = await TattooFileDialogs.Export(template);
            else
            {
                var result = await TattooFileDialogs.Import(template);
                if (_window != window || !window.IsOpen || _state?.Target != target) return;
                _status = result.Status;
                if (result.Tattoo == null) return;
                if (_state.Revision != revision || _pending || !IoCManager.Resolve<IConfigurationManager>().GetCVar(TattooCVars.Enabled)
                    || !_state.Tattoos.Any(t => t.ContentEquals(template)))
                { _status = "Настройки или рисунок изменились. Повторите загрузку PNG."; return; }
                var next = Tattoo.Sanitize(_state.Tattoos); next[next.FindIndex(t => t.Id == template.Id)] = result.Tattoo;
                _pending = true; SendMessage(new TattooMachineApplyMessage(next, revision));
            }
        }
        finally { _fileBusy = false; if (_window == window && window.IsOpen) Push(); }
    }
}
