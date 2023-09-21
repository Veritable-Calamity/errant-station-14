using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Wounds.Systems;

public sealed partial class WoundSystem
{
    /// <summary>
    /// Used internally by wound system to apply hpcap and intcap damage to the woundable
    /// </summary>
    /// <param name="woundEntity">target entity to add woundcap damage to</param>
    /// <param name="wound">actual wound to apply</param>
    /// <param name="condition">target woundable to add the damage to</param>
    private void ApplyWoundCapDamage(EntityUid woundEntity, WoundComponent wound, ConditionComponent condition)
    {
        //apply wound damage to damagecaps
        condition.HitPointCap -= wound.Severity*wound.HitpointDamage;
        FixedPoint2.Clamp(condition.HitPointCap, 0, condition.HitpointCapMax);
        FixedPoint2.Clamp(condition.HitPoints, 0, condition.HitPointCap);
        condition.IntegrityCap -= wound.Severity*wound.IntegrityDamage;
        FixedPoint2.Clamp(condition.IntegrityCap, 0, condition.IntegrityCapMax);
        FixedPoint2.Clamp(condition.Integrity, 0, condition.IntegrityCap);
        Dirty(wound.Parent, condition);
        Dirty(woundEntity, wound);
    }
    /// <summary>
    /// Used internally by wound system to remove hpcap and intcap damage from the woundable
    /// </summary>
    /// <param name="condition"></param>
    /// <param name="wound"></param>
    /// <param name="heal"></param>
    private void RemoveWoundCapDamage(ConditionComponent condition, WoundComponent wound, bool heal = false)
    {
        if (heal)
        {
            condition.HitPointCap += wound.Severity*wound.HitpointDamage;
            FixedPoint2.Clamp(condition.HitPointCap, 0, condition.HitpointCapMax);
            FixedPoint2.Clamp(condition.HitPoints, 0, condition.HitPointCap);
            condition.IntegrityCap += wound.Severity*wound.IntegrityDamage;
            FixedPoint2.Clamp(condition.IntegrityCap, 0, condition.IntegrityCapMax);
            FixedPoint2.Clamp(condition.Integrity, 0, condition.IntegrityCap);
        }
        Dirty(wound.Parent, condition);
    }


   /// <summary>
   /// Main function for responding to damage being applied to a woundable, this is the central logic for how/when wounds
   /// get applied and removed. This also handles healing and destroying/gibbing woundables when integrity and HP reach 0
   /// </summary>
    private void OnDamageApplied(EntityUid target, ConditionComponent condition,
        bool damageIncreased, FixedPoint2 totalDamage, EntityUid? origin, DamageSpecifier damageDelta)
    {
        if (damageIncreased)
        {
            condition.HitPoints -= totalDamage;
            if (condition.HitPoints < 0)
            {
                //this is a plus because hitpoints is now the negative overflow
                condition.Integrity += condition.HitPoints;
                condition.HitPoints = 0; //reset hitpoints to 0
                if (condition.Integrity <= 0)
                {
                    var overflowPercentage = FixedPoint2.Abs(condition.Integrity)/totalDamage;
                    var childWoundables = new (EntityUid, ConditionComponent)[]{};
                    //iterate through all wound entities and remove them
                    var woundEntities = new HashSet<EntityUid>(condition.WoundEntities);
                    foreach (var woundEntity in woundEntities)
                    {
                        RemoveWound(woundEntity, null, null);
                    }
                    //iterate through attached bodyparts to fix woundable roots and fix child->root references
                    if (TryComp<BodyPartComponent>(target, out var bodyPart))
                    {
                        //get all adjacent woundables and cache them for later
                        childWoundables = _body.GetBodyPartAdjacentPartsComponents<ConditionComponent>(target, bodyPart)
                            .ToArray();
                        FixWoundableRootsInDetachedPart(bodyPart, target, woundableRoot, origin);
                        foreach (var (_,slot) in bodyPart.Children)
                        {
                            if (slot.Child == null)
                                continue;
                            _body.OrphanPart(slot.Child, bodyPart);
                        }
                        //gib the bodypart :)
                        _body.GibPart(target, bodyPart, true, true);
                    }

                    if (childWoundables.Length <= 0)
                        return;
                    //if we have adjacent woundables calculate how many overflow damage gets applied to each and apply
                    // the damage
                    var overflowPercentagePerAdjacent = overflowPercentage / childWoundables.Length;
                    var damagePerAdjacent = new DamageSpecifier(damageDelta);
                    damagePerAdjacent.MultiplyTotalByModifier(overflowPercentagePerAdjacent);
                    foreach (var (childWoundableId,childWoundable) in childWoundables)
                    {
                        _damageable.TryChangeDamage(childWoundableId, damagePerAdjacent,
                            false, true, null, origin);
                    }
                }
                return;
            }
            CreateWoundsOnTarget(target, condition, woundableRoot, damageDelta, origin);
        }
        else
        {
            //TODO: healing goes here
        }
    }
}
