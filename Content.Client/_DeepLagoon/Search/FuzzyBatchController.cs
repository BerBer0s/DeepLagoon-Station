using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._DeepLagoon.Search;

/// <summary>Advances the batched searches once per frame; it does nothing while none is running.</summary>
public sealed class FuzzyBatchController : UIController
{
    public override void FrameUpdate(FrameEventArgs args)
    {
        FuzzyBatchRunner.Run();
    }
}
