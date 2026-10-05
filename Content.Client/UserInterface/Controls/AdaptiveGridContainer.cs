using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>Wrap fixed-width cards using the space offered by the parent.</summary>
public sealed class AdaptiveGridContainer : GridContainer
{
    private float _width;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var width = float.IsFinite(availableSize.X) ? Math.Max(1, availableSize.X - 12) : 640;
        if (Math.Abs(width - _width) > 0.5f)
        {
            _width = width;
            MaxGridWidth = width;
        }
        return base.MeasureOverride(availableSize);
    }
}
