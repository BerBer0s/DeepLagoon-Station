using Content.Shared._DeepLagoon.Mapping;
using Content.Shared.Hands.Components;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._DeepLagoon.Mapping;

/// <summary>
/// Keeps the placement ghost in sync with the entry selected on the held mapper RCD. The ghost is what
/// makes the engine rotate the placement direction on the rotate key.
/// </summary>
public sealed partial class MapperRcdGhostSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private MapperRcdSystem _catalog = default!;

    private readonly string _placementMode = typeof(AlignMapperRcd).Name;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var placerEntity = _placement.CurrentPermission?.MobUid;
        var placerIsMapper = HasComp<MapperRcdComponent>(placerEntity);

        // Another tool or the build menu owns the placement.
        if (_placement.Eraser || placerEntity != null && !placerIsMapper)
            return;

        if (!TryComp(_player.LocalEntity, out HandsComponent? hands) ||
            hands.ActiveHand?.HeldEntity is not { } held ||
            !TryComp(held, out MapperRcdComponent? rcd) ||
            rcd.SelectedEntry is not { } selected ||
            !_catalog.TryGetEntry(selected, out var entry) ||
            entry.Prototype == null)
        {
            if (placerIsMapper)
                _placement.Clear();

            return;
        }

        if (placerEntity == held && _placement.CurrentPermission?.EntityType == entry.Prototype)
            return;

        _placement.Clear();
        _placement.BeginPlacing(new PlacementInformation
        {
            MobUid = held,
            PlacementOption = _placementMode,
            EntityType = entry.Prototype,
            Range = (int) Math.Ceiling(rcd.Range),
            IsTile = entry.Mode == MapperRcdMode.Tile,
            UseEditorContext = false,
        });
    }
}
