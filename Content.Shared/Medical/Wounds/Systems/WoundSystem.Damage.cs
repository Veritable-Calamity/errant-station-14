using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Wounds.Systems;

public partial class WoundSystem
{

    private void InitDamage()
    {
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnWoundableDamaged);
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnWoundableDamaged);
    }

    private void OnWoundableDamaged(EntityUid target, WoundableComponent woundable,  DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
        {
            Log.Error($"Tried to set damage directly on {target} {woundable} do not set damage directly on entities " +
                      $"that are handled by woundables otherwise everything will explode");
            return;
        }

        if (woundable.AllowWounds)
        {
            ApplyDamageAndCreateWounds(target, woundable, args.DamageDelta);
            return;
        }

        if (!TryGetRandomActiveChildWoundable(target, out var data, woundable))
        {
            Log.Verbose($"Could not find a woundable to relay wounds to in {woundable}. If this expected ignore this warning," +
                        $" otherwise double check your woundable hierarchy!");
            return;
        }

        var childEntity = data.Value.Item1;
        if (_damageable.TryChangeDamage(childEntity, args.DamageDelta,
                interruptsDoAfters: args.InterruptsDoAfters, origin: args.Origin) != null)
            return;
        Log.Error($"Failed to relay damage to a woundable entity {childEntity} that does NOT have a damagable component. " +
                  $"This is required for wounds to function!");
    }

    protected void ApplyDamageAndCreateWounds(EntityUid target, WoundableComponent woundable, DamageSpecifier damage)
    {
        if (ApplyWoundableDamage(target, woundable, damage, out var overflow))
        {
            //TODO: implement bodypart destruction and damage overflow to surrounding parts
        }

        foreach (var (damageType, rawDamage) in damage.DamageDict)
        {
            var woundPool = GetWoundPoolFromDamageType(target, damageType, woundable);
            if (woundPool == null)
                continue;
            var woundProtoId =
                GetWoundProtoFromDamage(woundPool, CalculateWoundPercentDamage(target, rawDamage, woundable));
            if (!TrySpawnWound(target, woundProtoId, out var data))
            {
                Log.Error("Wound Creation failed!");
            }
        }
    }

    private bool ApplyWoundableDamage(EntityUid target, WoundableComponent woundable, DamageSpecifier damage,
        out FixedPoint2 overflow)
    {
        overflow = 0;
        woundable.HitPoints -= woundable.DamageScaling * damage.Total; //TODO factor in resistances when they get implemented
        if (woundable.HitPoints < 0)
        {
            woundable.Integrity -= woundable.HitPoints;
            woundable.HitPoints = 0;
            if (woundable.Integrity < 0)
            {
                overflow = -woundable.Integrity;
                woundable.Integrity = 0;
                //TODO: Destroy the wound and part here!
                return true;
            }
        }
        Dirty(target, woundable);
        return false;
    }

    public FixedPoint2 CalculateWoundPercentDamage(EntityUid target, FixedPoint2 damage, WoundableComponent? woundable)
    {
        if (!Resolve(target, ref woundable))
            return 0;
        var scaledDamage = woundable.DamageScaling * damage;
        return scaledDamage / woundable.TotalCapMax;
    }
}
