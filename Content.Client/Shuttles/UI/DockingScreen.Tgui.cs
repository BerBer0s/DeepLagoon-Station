using System.Linq;
using Content.Client._DeepLagoon.WebUI;

namespace Content.Client.Shuttles.UI;

public sealed partial class DockingScreen
{
    public ShuttleDockControl TguiRadar => DockingControl;
    public TguiData TguiState()
    {
        var state = new TguiData().Bool("undockDisabled", UndockAllButton.Disabled)
            .Bool("locked", FTLLockEnabledButton.Pressed);
        if (DockingControl.GridEntity is not { } grid || !Docks.TryGetValue(_entManager.GetNetEntity(grid), out var docks))
            return state.Array("ports", Array.Empty<TguiData>());
        return state.Array("ports", docks.Select(dock => new TguiData().String("id", dock.Entity.ToString())
            .String("name", dock.LabelName ?? dock.Name).Bool("connected", dock.Connected)
            .Bool("selected", DockingControl.ViewedDock == dock.Entity)));
    }

    public void TguiAction(TguiActionData args)
    {
        if (DockingControl.GridEntity is not { } grid) return;
        switch (args.String("id"))
        {
            case "undockAll": if (!UndockAllButton.Disabled) OnUndockAllPressed(); return;
            case "lock": OnFTLLockPressed(true); return;
            case "unlock": OnFTLLockPressed(false); return;
        }
        if (!Docks.TryGetValue(_entManager.GetNetEntity(grid), out var docks)) return;
        foreach (var dock in docks)
            if (dock.Entity.ToString() == args.String("id")) { OnDockPress(dock); break; }
    }
}
