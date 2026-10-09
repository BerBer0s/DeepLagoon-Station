using Robust.Shared.Serialization;

namespace Content.Shared._DeepLagoon.GridMagnet;

/// <summary>
/// Appearance data keys of the grid magnet.
/// </summary>
[Serializable, NetSerializable]
public enum GridMagnetVisuals : byte
{
    /// <summary>
    /// Bool: whether the machine is active.
    /// </summary>
    Active,
}

/// <summary>
/// Sprite layers of the grid magnet, mapped in the prototype so the art can be swapped without code changes.
/// </summary>
[Serializable, NetSerializable]
public enum GridMagnetVisualLayers : byte
{
    /// <summary>
    /// The machine body, always visible.
    /// </summary>
    Base,

    /// <summary>
    /// Shown only while the machine is active.
    /// </summary>
    Active,
}
