using Content.Client.Construction;
using Content.Client.ContextMenu.UI;
using Content.Client.Interaction;
using Content.Client.Tabletop;
using Content.Shared._DeepLagoon.Mapping;
using Content.Shared.Atmos.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Content.Shared.RCD.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.Mapping;

/// <summary>
/// Turns holding the use button with a mapper RCD into a brush stroke. Tiles the cursor passes over are
/// collected (gaps between frames are filled with a Bresenham line), each tile once per stroke, and sent to
/// the server in batches at most once per tick. The primary button paints with the selected entry, the
/// secondary button always removes structures. A click is swallowed only while the tool is in the active
/// hand with an entry selected, so the item never gets in the way of normal interaction otherwise.
/// </summary>
public sealed partial class MapperRcdBrushSystem : EntitySystem
{
    /// <summary>
    /// Upper bound of distinct tiles in one stroke.
    /// </summary>
    private const int MaxStrokeCells = 16384;

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private MapperRcdSystem _catalog = default!;
    [Dependency] private RCDSystem _rcd = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly HashSet<Vector2i> _visited = new();
    private readonly List<Vector2i> _pending = new();

    private bool _active;
    private EntityUid _tool;
    private EntityUid _grid;
    private BoundKeyFunction _key = EngineKeyFunctions.Use;
    private string _selected = string.Empty;
    private string _entryId = string.Empty;
    private AtmosPipeLayer _pipeLayer;
    private Vector2i _last;
    private GameTick _lastFlushTick;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;

        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Use,
                new PointerInputCmdHandler((in PointerInputCmdHandler.PointerInputCmdArgs args) => OnKey(args, EngineKeyFunctions.Use),
                    ignoreUp: false, outsidePrediction: true),
                typeof(ConstructionSystem), typeof(DragDropSystem))
            .BindBefore(EngineKeyFunctions.UseSecondary,
                new PointerInputCmdHandler((in PointerInputCmdHandler.PointerInputCmdArgs args) => OnKey(args, EngineKeyFunctions.UseSecondary),
                    ignoreUp: false, outsidePrediction: true),
                typeof(EntityMenuUIController), typeof(TabletopSystem))
            .Register<MapperRcdBrushSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<MapperRcdBrushSystem>();
    }

    /// <summary>
    /// Drops the current stroke without sending what has not been sent yet.
    /// </summary>
    public void CancelStroke()
    {
        _active = false;
        _visited.Clear();
        _pending.Clear();
    }

    private bool OnKey(in PointerInputCmdHandler.PointerInputCmdArgs args, BoundKeyFunction key)
    {
        switch (args.State)
        {
            case BoundKeyState.Up:
                if (!_active || _key != key)
                    return false;

                Flush();
                CancelStroke();
                return true;

            case BoundKeyState.Down:
                return BeginStroke(args, key);

            default:
                return false;
        }
    }

    private bool BeginStroke(in PointerInputCmdHandler.PointerInputCmdArgs args, BoundKeyFunction key)
    {
        CancelStroke();

        if (!TryGetBrush(out var player, out var tool, out var selected))
            return false;

        if (!args.Coordinates.IsValid(EntityManager) ||
            !_rcd.TryGetMapGridData(args.Coordinates, out var gridData))
        {
            _popup.PopupClient(Loc.GetString("mapper-rcd-no-grid"), tool, player);
            return true;
        }

        _active = true;
        _tool = tool;
        _key = key;
        _selected = selected;

        // The secondary button erases whatever entry is selected; pipes remember the layer picked by the cursor.
        _entryId = key == EngineKeyFunctions.UseSecondary ? MapperRcdComponent.DeconstructEntryId : selected;
        _pipeLayer = ReadPipeLayer(selected);
        _grid = gridData.Value.GridUid;

        var world = _transform.ToMapCoordinates(args.Coordinates).Position;
        _last = _map.WorldToTile(_grid, gridData.Value.Component, world);
        AddCell(_last);
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_active)
            return;

        if (!_clyde.IsFocused ||
            _inputSystem.CmdStates.GetState(_key) != BoundKeyState.Down ||
            !TryGetBrush(out _, out var tool, out var selected) ||
            tool != _tool ||
            selected != _selected ||
            !TryComp(_grid, out MapGridComponent? grid))
        {
            CancelStroke();
            return;
        }

        var mouse = _eye.PixelToMap(_input.MouseScreenPosition);
        if (mouse.MapId == Transform(_grid).MapID)
            AdvanceTo(_map.WorldToTile(_grid, grid, mouse.Position));

        if (_timing.CurTick > _lastFlushTick)
            Flush();
    }

    private bool TryGetBrush(out EntityUid player, out EntityUid tool, out string selected)
    {
        player = default;
        tool = default;
        selected = string.Empty;

        if (_player.LocalEntity is not { } local ||
            !TryComp(local, out HandsComponent? hands) ||
            hands.ActiveHand?.HeldEntity is not { } held ||
            !TryComp(held, out MapperRcdComponent? rcd) ||
            rcd.SelectedEntry is not { } chosen ||
            !_catalog.TryGetEntry(chosen, out _))
        {
            return false;
        }

        player = local;
        tool = held;
        selected = chosen;
        return true;
    }

    private AtmosPipeLayer ReadPipeLayer(string selected)
    {
        // The pipe placement ghost swaps the placed prototype for the layer under the cursor.
        if (!_catalog.TryGetEntry(selected, out var entry) ||
            entry.Slot != MapperRcdSlot.Pipe ||
            _placement.CurrentPermission?.EntityType is not { } id ||
            !_protos.TryIndex<EntityPrototype>(id, out var proto) ||
            !proto.TryGetComponent<AtmosPipeLayersComponent>(out var layers, EntityManager.ComponentFactory))
        {
            return AtmosPipeLayer.Primary;
        }

        return layers.CurrentPipeLayer;
    }

    private void AdvanceTo(Vector2i target)
    {
        var x = _last.X;
        var y = _last.Y;
        var dx = Math.Abs(target.X - x);
        var dy = -Math.Abs(target.Y - y);
        var sx = x < target.X ? 1 : -1;
        var sy = y < target.Y ? 1 : -1;
        var err = dx + dy;

        while (x != target.X || y != target.Y)
        {
            var e2 = 2 * err;

            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }

            AddCell(new Vector2i(x, y));
        }

        _last = target;
    }

    private void AddCell(Vector2i cell)
    {
        if (_visited.Count < MaxStrokeCells && _visited.Add(cell))
            _pending.Add(cell);
    }

    private void Flush()
    {
        _lastFlushTick = _timing.CurTick;

        if (_pending.Count == 0)
            return;

        var tool = GetNetEntity(_tool);
        var grid = GetNetEntity(_grid);
        var direction = _placement.Direction;

        for (var i = 0; i < _pending.Count; i += MapperRcdStrokeEvent.MaxCells)
        {
            var batch = new List<Vector2i>(MapperRcdStrokeEvent.MaxCells);
            var end = Math.Min(i + MapperRcdStrokeEvent.MaxCells, _pending.Count);

            for (var j = i; j < end; j++)
            {
                batch.Add(_pending[j]);
            }

            RaiseNetworkEvent(new MapperRcdStrokeEvent(tool, grid, _entryId, direction, _pipeLayer, batch));
        }

        _pending.Clear();
    }
}
