using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Crescent.ShipShields;

public sealed partial class ShipShieldOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    private ShipShieldOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ShipShieldOverlay(EntityManager, _prototypeManager);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<ShipShieldOverlay>();
        _overlay?.Dispose();
        _overlay = null;
    }
}
