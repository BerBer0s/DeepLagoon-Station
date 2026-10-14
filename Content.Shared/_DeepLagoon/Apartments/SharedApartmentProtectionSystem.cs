using System.Linq;
using Content.Shared.Damage;
using static Content.Shared.Damage.DamageableSystem;

namespace Content.Shared._DeepLagoon.Apartments;

public sealed class SharedApartmentProtectionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageableComponent, BeforeDamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, DamageableComponent comp, ref BeforeDamageChangedEvent args)
    {
        // Healing remains possible; belongings, structures and visitors are protected equally.
        if (HasComp<ApartmentProtectedGridComponent>(Transform(uid).GridUid) && args.Damage.DamageDict.Values.Any(value => value > 0))
            args.Cancelled = true;
    }
}
