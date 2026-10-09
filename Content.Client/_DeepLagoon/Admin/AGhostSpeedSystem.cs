using Content.Client.Viewport;
using Content.Shared._DeepLagoon.Admin;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._DeepLagoon.Admin;

/// <summary>
/// Turns Shift + mouse wheel over the main viewport into speed step requests for the admin ghost.
/// The wheel is not a bindable key in this engine, so <see cref="ScalingViewport"/> forwards it here.
/// The server decides everything; this only gates on "the local entity is an admin ghost" to avoid useless traffic.
/// </summary>
public sealed partial class AGhostSpeedSystem : SharedAGhostSpeedSystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;

    /// <summary>
    /// Touchpads send many small deltas; a notch is sent once they add up to a whole one.
    /// </summary>
    private float _accumulated;

    private const int MaxStepsPerEvent = 4;

    public void OnViewportWheel(ScalingViewport viewport, GUIMouseWheelEventArgs args)
    {
        if (!ReferenceEquals(_eye.MainViewport, viewport) ||
            !_input.IsKeyDown(Keyboard.Key.Shift) ||
            _player.LocalEntity is not { } uid ||
            !HasComp<AGhostSpeedComponent>(uid))
        {
            return;
        }

        args.Handle();

        var delta = args.Delta.Y;
        if (delta == 0f)
            return;

        // A reversed direction drops what was left over from the other one.
        if (MathF.Sign(delta) != MathF.Sign(_accumulated))
            _accumulated = 0f;

        _accumulated += delta;

        for (var i = 0; i < MaxStepsPerEvent && MathF.Abs(_accumulated) >= 1f; i++)
        {
            RaiseNetworkEvent(new AGhostSpeedStepEvent(_accumulated > 0f));
            _accumulated -= MathF.Sign(_accumulated);
        }
    }
}
