using System.Linq;
using Content.Client._DeepLagoon.WebUI;

namespace Content.Client._Mono.FireControl.UI;

public sealed partial class FireControlPanel
{
    public TguiData TguiState() => new TguiData().String("status", ServerStatus.Text)
        .Bool("hasWeapons", _currentState is { Connected: true, FireControllables.Length: > 0 })
        .Bool("available", _currentState != null).Array("entries", WeaponsList.Select(pair =>
            new TguiData().String("id", pair.Key.ToString()).String("name", pair.Value.Text)
                .Bool("selected", pair.Value.Pressed).Bool("disabled", pair.Value.Disabled)));

    public void TguiAction(TguiActionData args)
    {
        switch (args.String("id"))
        {
            case "refresh": OnServerRefresh?.Invoke(); return;
            case "all": SelectAllWeapons(null!); return;
            case "none": UnselectAllWeapons(null!); return;
            case "ballistic": SelectBallisticWeapons(null!); return;
            case "energy": SelectEnergyWeapons(null!); return;
            case "missile": SelectMissileWeapons(null!); return;
        }
        foreach (var (entity, button) in WeaponsList)
        {
            if (entity.ToString() != args.String("id") || button.Disabled) continue;
            button.Pressed ^= true;
            OnWeaponSelectionChanged?.Invoke();
            UpdateAllWeaponButtonTexts();
            break;
        }
    }
}
