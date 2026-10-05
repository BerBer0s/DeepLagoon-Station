using System.Numerics;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Wagging;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Wagging;

/// <summary>Animate existing tail layers when there is no dedicated wagging RSI.</summary>
public sealed class WaggingSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    private float _phase;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _phase = (_phase + frameTime * 7f) % MathF.Tau;
        var query = EntityQueryEnumerator<WaggingComponent, HumanoidAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var wag, out var humanoid, out var sprite))
        {
            if (!humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
                continue;
            foreach (var marking in markings)
            {
                if (!_prototypes.TryIndex<MarkingPrototype>(marking.MarkingId, out var proto) || proto.BodyPart != HumanoidVisualLayers.Tail)
                    continue;
                foreach (var specifier in proto.Sprites)
                {
                    if (specifier is not SpriteSpecifier.Rsi rsi || !sprite.LayerMapTryGet($"{marking.MarkingId}-{rsi.RsiState}", out var layer))
                        continue;
                    // Animated variants use their artist-authored frames. Static tails move by two pixels.
                    var native = marking.MarkingId.EndsWith(wag.Suffix);
                    var offset = wag.Wagging && !native ? MathF.Sin(_phase + uid.Id % 7) * (2f / 32f) : 0f;
                    _sprites.LayerSetOffset((uid, sprite), layer, new Vector2(offset, 0));
                    _sprites.LayerSetAutoAnimated((uid, sprite), layer, wag.Wagging);
                    if (!wag.Wagging)
                        _sprites.LayerSetAnimationTime((uid, sprite), layer, 0);
                }
            }
        }
    }
}
