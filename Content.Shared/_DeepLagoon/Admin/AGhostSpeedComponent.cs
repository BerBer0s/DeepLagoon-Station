using Robust.Shared.GameStates;

namespace Content.Shared._DeepLagoon.Admin;

/// <summary>
/// Marks the admin ghost (<c>AdminObserver</c>) and holds its speed multiplier, changed with Shift + mouse wheel.
/// The multiplier is applied through <c>RefreshMovementSpeedModifiersEvent</c>, so it stacks with other modifiers and
/// never touches the base speeds. A new admin ghost is a new entity, so it always starts at 1.
/// While this component is present the Walk key does nothing: Shift belongs to the speed wheel.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AGhostSpeedComponent : Component
{
    /// <summary>
    /// Current multiplier of the base speed. Only the server writes it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;

    /// <summary>
    /// Smallest allowed multiplier.
    /// </summary>
    [DataField]
    public float Min = 0.25f;

    /// <summary>
    /// Largest allowed multiplier.
    /// </summary>
    [DataField]
    public float Max = 8f;

    /// <summary>
    /// Factor applied by one wheel notch: multiplied going up, divided going down.
    /// </summary>
    [DataField]
    public float Step = 1.25f;

    /// <summary>
    /// Server only. When the speed popup is due: it is shown once after the wheel stops, not on every notch.
    /// </summary>
    [ViewVariables]
    public TimeSpan? PopupAt;
}
