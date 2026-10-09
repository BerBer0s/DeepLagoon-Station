using Robust.Shared.GameStates;

namespace Content.Shared._DeepLagoon.Mapping;

/// <summary>
/// Unlimited, instant construction brush for mappers. Painting with the held button is
/// handled by the client and applied by the server in batches of cells.
/// Deliberately independent from <c>RCDComponent</c>: no charges, no cooldown, no do-after.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MapperRcdComponent : Component
{
    /// <summary>
    /// Id of the catalog entry (see <see cref="MapperRcdEntry"/>) chosen in the radial menu.
    /// Null means nothing is selected and the item behaves like a plain item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SelectedEntry;

    /// <summary>
    /// Id of the catalog entry that removes structures. The secondary button always paints with it.
    /// </summary>
    public const string DeconstructEntryId = "MapperDeconstruct";

    /// <summary>
    /// Maximum distance in tiles between the user and a painted tile. Obstructions are not checked.
    /// </summary>
    [DataField]
    public float Range = 50f;
}
