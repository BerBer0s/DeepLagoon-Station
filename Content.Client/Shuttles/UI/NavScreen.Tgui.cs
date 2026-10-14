using System.Linq;
using Content.Client._DeepLagoon.WebUI;
using Content.Shared._NF.Shuttles.Events;

namespace Content.Client.Shuttles.UI;

public sealed partial class NavScreen
{
    public TguiData TguiState() => new TguiData()
        .String("name", NavDisplayLabel.Text).String("designation", ShuttleDesignation.Text)
        .String("position", GridPosition.Text).String("azimuth", GridOrientation.Text)
        .String("velocity", GridLinearVelocity.Text).String("angular", GridAngularVelocity.Text)
        .Bool("zLevel", GridAltitude.Visible).String("altitude", GridAltitude.Text)
        .String("vertical", GridVerticalVelocity.Text).String("travel", GridTravelState.Text)
        .Bool("iff", NavRadar.ShowIFF).Bool("details", NavRadar.ShowIFFDetailed).Bool("docks", NavRadar.ShowDocks)
        .Number("dampener", (int)NavRadar.DampeningMode).Bool("station", !DampenerModeButtons.Visible)
        .Bool("ftl", NavRadar.InFtl).String("search", IffSearchCriteria.Text)
        .Number("range", NavRadar.MaximumIFFDistance).String("speed", MaximumShuttleSpeedValue.Text)
        .Array("ports", new[] { DeviceButton1, DeviceButton2, DeviceButton3, DeviceButton4,
            DeviceButton5, DeviceButton6, DeviceButton7, DeviceButton8 }.Select((button, i) =>
                new TguiData().Number("id", i + 1).String("name", button.Text).Bool("disabled", button.Disabled)));

    public void TguiAction(TguiActionData args)
    {
        switch (args.String("id"))
        {
            case "iff": NavRadar.ShowIFF ^= true; break;
            case "details": NavRadar.ShowIFFDetailed ^= true; break;
            case "docks": NavRadar.ShowDocks ^= true; break;
            case "search":
                IffSearchCriteria.Text = (args.String("value") ?? "")[..Math.Min(128, (args.String("value") ?? "").Length)];
                OnIffSearchChanged(IffSearchCriteria.Text); break;
            case "range":
                if (args.TryFloat("value", out var range)) NavRadar.MaximumIFFDistance = Math.Clamp(range, 0, 30000);
                break;
            case "speed":
                if (string.IsNullOrWhiteSpace(args.String("value")))
                {
                    MaximumShuttleSpeedValue.Text = "";
                    OnMaxShuttleSpeedChanged?.Invoke(null);
                    break;
                }
                if (args.TryFloat("value", out var speed))
                {
                    speed = MathF.Max(0, speed);
                    MaximumShuttleSpeedValue.Text = speed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    OnMaxShuttleSpeedChanged?.Invoke(speed);
                }
                break;
            case "dampener":
                if (DampenerModeButtons.Visible && args.TryInt("value", out var mode)
                    && mode is >= 0 and <= 2 && !(NavRadar.InFtl && mode == (int)InertiaDampeningMode.Anchor))
                    SetDampenerMode((InertiaDampeningMode)mode);
                break;
            case "port":
                if (args.TryInt("value", out var port) && port is >= 1 and <= 8)
                    OnPortButtonPressed($"device-button-{port}", $"button-{port}");
                break;
        }
    }
}
