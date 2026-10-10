using System.Numerics;
using Content.Client._DeepLagoon.WebUI;
using Content.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.Lathe;

public sealed class LatheTguiWindow : FancyWindow
{
    public readonly TguiPanel Panel = new();

    /// <summary>Raised every frame while the window is open, with the seconds since the last one.</summary>
    public event Action<float>? Frame;

    public LatheTguiWindow()
    {
        SetSize = new Vector2(1000, 640);
        MinSize = new Vector2(760, 480);
        ContentsContainer.AddChild(Panel);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        Frame?.Invoke(args.DeltaSeconds);
    }
}
