using System.Numerics;
using Robust.Client.UserInterface;

namespace Content.Client._DeepLagoon.Spawn;

/// <summary>Keeps the reference spawn button out of the window's minimum size.</summary>
public sealed class SpawnMeasureContainer : Control
{
    protected override Vector2 MeasureOverride(Vector2 availableSize) => Vector2.Zero;
}
