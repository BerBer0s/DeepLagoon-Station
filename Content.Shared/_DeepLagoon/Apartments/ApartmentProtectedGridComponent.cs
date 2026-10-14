using Robust.Shared.GameStates;

namespace Content.Shared._DeepLagoon.Apartments;

/// <summary>Residential grid: protection follows its contents without per-object components.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ApartmentProtectedGridComponent : Component;
