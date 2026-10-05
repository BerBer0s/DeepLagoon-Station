using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>Restore the saved split after children have been measured.</summary>
public sealed class RecordedSplitContainer : SplitContainer
{
    public float? DesiredSplitCenter;
    private float _fraction = 0.65f;
    private float _lastWidth;

    public RecordedSplitContainer()
    {
        OnSplitResizeFinished += () => _fraction = SplitFraction;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        if (finalSize.X > 0 && Size.X > 0 && (DesiredSplitCenter.HasValue || Math.Abs(finalSize.X - _lastWidth) > 0.5f))
        {
            _fraction = DesiredSplitCenter ?? _fraction;
            SplitCenter = _fraction * finalSize.X;
            DesiredSplitCenter = null;
            _lastWidth = finalSize.X;
        }
        return base.ArrangeOverride(finalSize);
    }
}
