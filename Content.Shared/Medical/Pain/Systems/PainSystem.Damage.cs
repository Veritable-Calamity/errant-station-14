using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Pain.Components;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Pain.Systems;


//For handling generic pain unrelated to wounds
public sealed partial class PainSystem
{
    private void InitializeDamage()
    {
        SubscribeLocalEvent<NerveComponent, DamageChangedEvent>(OnNerveRecievedDamage);
    }

    /// <summary>
    ///  Relays pain damage to the nervous system to be applied as generic pain
    /// </summary>
    private void OnNerveRecievedDamage(EntityUid woundableEntity, NerveComponent nerve, DamageChangedEvent args)
    {
        if (args.DamageDelta == null || args.DamageDelta.DamageDict.TryGetValue("Pain", out var damage) ||
            !TryComp<WoundableComponent>(woundableEntity, out var woundable)
            || !TryComp<NervousSystemComponent>(woundable.RootWoundable, out var nervousSystem))
            return;
        var oldPain = nervousSystem.Pain;
        nervousSystem.GenericPain += damage*nerve.PainMultiplier;

        var painChanged = new PainChangedEvent(woundableEntity, nervousSystem, oldPain, nervousSystem.Pain - oldPain);
        RaiseLocalEvent(woundable.RootWoundable, ref painChanged);
        Dirty(woundable.RootWoundable, nervousSystem);
    }

    /// <summary>
    /// Sets the generic pain on the nervous system entity, this does not raise damage changed events
    /// because you should not be listening for pain being changed through damagable! Use pain events instead!
    /// </summary>
    /// <param name="nervousSystemEntity">owning entity of the nervous system</param>
    /// <param name="newGenericPain">new generic pain value</param>
    /// <param name="nervousSystemComponent"></param>
    public void SetGenericPainOnWoundableRoot(EntityUid nervousSystemEntity, FixedPoint2 newGenericPain,
        NervousSystemComponent? nervousSystemComponent = null)
    {
        if (!Resolve(nervousSystemEntity, ref nervousSystemComponent) || TryComp<WoundableComponent>(nervousSystemEntity, out var woundable))
            return;
        nervousSystemComponent.GenericPain = newGenericPain;

        var rootDamageable = Comp<DamageableComponent>(nervousSystemEntity);
        rootDamageable.Damage.DamageDict["Pain"] = newGenericPain; //Set new generic pain value on our root

        foreach (var (childWoundableEntity, childWoundable) in _woundSystem.GetAllWoundableChildren(nervousSystemEntity,woundable))
        {
            var damageable = Comp<DamageableComponent>(childWoundableEntity);
            damageable.Damage.DamageDict["Pain"] = 0; //Reset pain damage to zero without calling update to avoid raising garbage events.
            Dirty(childWoundableEntity, damageable);
        }
        Dirty(nervousSystemEntity, rootDamageable);
        Dirty(nervousSystemEntity, nervousSystemComponent);
    }
}
