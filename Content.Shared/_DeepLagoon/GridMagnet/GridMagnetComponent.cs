using Robust.Shared.GameStates;

namespace Content.Shared._DeepLagoon.GridMagnet;

/// <summary>
/// A stationary directional machine that pulls two separate grids together and welds them into one grid.
/// Two machines on different grids that are active, in range and face each other start a pull; the grid that
/// is not kept moves until the machine tiles are adjacent, waits <see cref="WeldTime"/>, and is then merged
/// into the kept grid. The front of the machine is the side its sprite faces (south at rotation zero).
/// All tuning lives here so it can be changed with View Variables in game. When two machines pair up, the
/// values of the machine on the kept grid are used for the whole pull.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridMagnetComponent : Component
{
    /// <summary>
    /// Whether the machine is switched on. Only the server writes it. An unanchored machine is never active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// Largest distance in tiles between this machine and its partner for a pull to start.
    /// </summary>
    [DataField]
    public float Range = 40f;

    /// <summary>
    /// How far, in degrees, the two machines may be from facing exactly opposite each other.
    /// </summary>
    [DataField]
    public float AngleTolerance = 15f;

    /// <summary>
    /// Seconds between a valid pair being found and the grid starting to move.
    /// </summary>
    [DataField]
    public float StartDelay = 3f;

    /// <summary>
    /// Pull speed in tiles per second.
    /// </summary>
    [DataField]
    public float PullSpeed = 2f;

    /// <summary>
    /// Turn speed of the pulled grid in degrees per second.
    /// </summary>
    [DataField]
    public float PullRotationSpeed = 10f;

    /// <summary>
    /// The pull never takes less than this many seconds, however short the way is.
    /// </summary>
    [DataField]
    public float MinPullTime = 3f;

    /// <summary>
    /// A pull that would take longer than this many seconds is refused.
    /// </summary>
    [DataField]
    public float MaxPullTime = 180f;

    /// <summary>
    /// Seconds between the grids touching and the merge.
    /// </summary>
    [DataField]
    public float WeldTime = 10f;

    /// <summary>
    /// When set, the machine only works while its <c>ApcPowerReceiver</c> is powered, and a machine without
    /// one never works. Turn it off with View Variables to test without wiring a grid.
    /// </summary>
    [DataField]
    public bool RequirePower = true;

    /// <summary>
    /// Whether a grid that carries a ship deed may be merged. Off by default: the merged-away grid is deleted
    /// and the deed on the ID card would point at nothing.
    /// </summary>
    [DataField]
    public bool AllowDeededGrids;
}
