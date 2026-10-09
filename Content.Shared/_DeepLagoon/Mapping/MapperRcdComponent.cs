using Content.Shared.Interaction;
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
    /// Maximum distance between the user and a painted tile. Obstructions are not checked.
    /// </summary>
    [DataField]
    public float Range = SharedInteractionSystem.InteractionRange;
}
