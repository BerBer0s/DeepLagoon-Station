using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed partial class MapScreen
{
    public ShuttleMapControl TguiRadar => MapRadar;
    private int _tguiDestinationId;
    private readonly Dictionary<string, (Button Button, IMapObject Object)> _tguiDestinations = new();

    public TguiData TguiState()
    {
        MapRadar.SetMapObjects(_mapObjects);
        if (!IsFTLBlocked() && _nextPing < _timing.CurTime)
            MapRebuildButton.Disabled = false;
        var progress = _ftlTime.ProgressAt(_timing.CurTime);
        return new TguiData().String("status", MapFTLState.Text).Number("progress", float.IsFinite(progress) ? progress : 1)
        .Bool("ftl", MapFTLButton.Pressed).Bool("autopilot", MapAutopilotButton.Pressed)
        .Bool("disabled", MapFTLButton.Disabled).Bool("autopilotDisabled", MapAutopilotButton.Disabled)
        .Bool("refreshDisabled", MapRebuildButton.Disabled).Bool("beacons", MapRadar.ShowBeacons)
        .Array("destinations", _tguiDestinations.Select(pair => new TguiData().String("id", pair.Key)
            .String("name", pair.Value.Object.Name).Bool("disabled", pair.Value.Button.Disabled)));
    }

    public void TguiAction(TguiActionData args)
    {
        switch (args.String("id"))
        {
            case "ftl": if (!MapFTLButton.Disabled) SetTargeting(!MapFTLButton.Pressed, false); break;
            case "autopilot": if (!MapAutopilotButton.Disabled) SetTargeting(!MapAutopilotButton.Pressed, true); break;
            case "beacons": MapRadar.ShowBeacons ^= true; break;
            case "refresh": if (!MapRebuildButton.Disabled) MapRebuildPressed(null!); break;
            default:
                if (args.String("id") is { } id && _tguiDestinations.TryGetValue(id, out var entry) && !entry.Button.Disabled)
                    OnMapObjectPress(entry.Object);
                break;
        }
    }
}
