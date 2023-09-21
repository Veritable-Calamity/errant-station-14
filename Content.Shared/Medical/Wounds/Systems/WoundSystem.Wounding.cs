using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Medical.Wounds.Prototypes;
namespace Content.Shared.Medical.Wounds.Systems;

public partial class WoundSystem
{
    public List<(EntityUid, WoundComponent)>  CreateWoundsOnTarget(EntityUid target, ConditionComponent? woundable,
        WoundableRootComponent? woundableRoot, DamageSpecifier? damageSpecifier, EntityUid? origin = null)
    {
        List<(EntityUid, WoundComponent)> createdWounds = new();
        if (!Resolve(target, ref woundable)
            ||damageSpecifier == null
            || woundable.ParentWoundable == null
            || !Resolve(woundable.ParentWoundable.Value, ref woundableRoot)) //Return empty if there is no damage/damagable specifier
            return createdWounds;
        HashSet<string> usedDamageTypes = new();

        //===Add wounds for damage groups and track which damage types have already been used ===
        foreach (var (damageGroup,damage) in damageSpecifier.GetDamagePerGroup(_prototype))
        {
            if (!woundableRoot.WoundGroupPools.TryGetValue(damageGroup, out var woundPoolString))
                continue; //Do nothing if this damage type is not supported

            if (!_prototype.TryIndex<TraumaPrototype>(woundPoolString, out var woundPool))
            {
                Log.Error($"Wound pool: {woundPoolString} does not exist!");
                continue;
            }
            if (!_prototype.TryIndex<DamageGroupPrototype>(damageGroup, out var dmgGroup))
                continue; //if there is no damage group don't bother trying to create a wound (This should never happen)
            foreach (var dmgType in dmgGroup.DamageTypes)
            {
                usedDamageTypes.Add(dmgType);
            }
            var woundProtoId = GetWoundProtoIdFromPool(woundPool, damage, woundable.HitPoints, woundableRoot.DamageScaling);
            var createdWound = CreateWoundOnTarget(target, woundable, woundableRoot, woundProtoId, origin);
            if (createdWound == null)
                continue;
            createdWounds.Add((createdWound.Value.Item1, createdWound.Value.Item2));
        }
        //===Add wounds for isolated damage types only if they have not been added from damage groups ===
        foreach (var (damageType, damage) in damageSpecifier.DamageDict)
        {
            if (!woundableRoot.WoundGroupPools.TryGetValue(damageType, out var woundPoolString) || usedDamageTypes.Contains(damageType))
                continue; //Do nothing if this damage type is not supported or already used by a group
            if (!_prototype.TryIndex<TraumaPrototype>(woundPoolString, out var woundPool))
            {
                Log.Error($"Wound pool: {woundPoolString} does not exist!");
                continue;
            }
            var woundProtoId = GetWoundProtoIdFromPool(woundPool, damage, woundable.HitPoints, woundableRoot.DamageScaling);
            var createdWound = CreateWoundOnTarget(target, woundable, woundableRoot, woundProtoId, origin);
            if (createdWound == null)
                continue;
            createdWounds.Add((createdWound.Value.Item1, createdWound.Value.Item2));
        }
        return createdWounds;
    }


    private string GetWoundProtoIdFromPool(TraumaPrototype pool, FixedPoint2 damage, FixedPoint2 maxDamage,
        FixedPoint2 modifier)
    {
        var adjustedMax = maxDamage / modifier;
        foreach (var (requiredPercentage, woundProtoId) in pool.Wounds)
        {
            var percentage = damage / adjustedMax;
            if (percentage >= 100)
            {
                return pool.Wounds.Last().Value;
            }
            if (percentage > requiredPercentage)
            {
                continue;
            }
            return woundProtoId;
        }
        return pool.Wounds.Last().Value;
    }

    public (EntityUid, WoundComponent)? CreateWoundOnTarget(EntityUid target, ConditionComponent? woundable,
        WoundableRootComponent? woundableRoot, string woundProtoId, EntityUid? origin = null)
    {
        //validate all component and entity parameters
        if (!Resolve(target, ref woundable)
            || woundable.ParentWoundable == null
            || !Resolve(woundable.ParentWoundable.Value,
                ref woundableRoot)) //Return empty if there is no damage/damagable specifier
            return null;
        //raise a broadcast event saying that we are adding a wound
        var tryApplyWound = new TryApplyWoundEvent(target, woundable, true,origin);
        RaiseLocalEvent(target, ref tryApplyWound, true);
        if (!tryApplyWound.ShouldApply)
        {
            return null; //block the wound from being created and applied
        }

        var woundEntity = Spawn(woundProtoId, Transform(target).Coordinates);
        var wound = Comp<WoundComponent>(woundEntity);
        wound.Parent = target;
        _transform.SetParent(woundEntity, target);
        woundable.WoundEntities.Add(target);
        ApplyWoundCapDamage(woundEntity, wound, woundable);
        //create and raise the wound applied event
        var woundAdded = new WoundAppliedEvent(woundEntity, wound, origin);
        RaiseLocalEvent(target, ref woundAdded);
        RaiseLocalEvent(woundEntity, ref woundAdded);
       return (woundEntity,wound);
    }

    public void RemoveWound(EntityUid woundEntity, WoundComponent? wound, ConditionComponent? woundable ,
        bool heal = false, bool scar = false, EntityUid? origin = null)
    {
        if (!Resolve(woundEntity, ref wound))
            return;
        //create and raise the wound cleared event
        var woundCleared= new WoundClearedEvent(woundEntity, wound, origin);
        RaiseLocalEvent(wound.Parent, ref woundCleared);
        RaiseLocalEvent(woundEntity, ref woundCleared);
        if (Resolve(wound.Parent, ref woundable))
        {
            woundable.WoundEntities.Remove(woundEntity);
            RemoveWoundCapDamage(woundable, wound, heal);
            //create and apply scar wound if scarring is enabled and a scar is specified
            if (scar && wound.ScarWound != null)
                CreateWoundOnTarget(wound.Parent, woundable, null, wound.ScarWound, origin);
            Dirty(wound.Parent, woundable);
        }
        Del(woundEntity);
    }


    public void SetWoundableParent(EntityUid rootEntity, WoundableRootComponent? woundableRoot,
        EntityUid woundableEntity, ConditionComponent? woundable)
    {
        if (!Resolve(rootEntity, ref woundableRoot) ||
            !Resolve(woundableEntity, ref woundable))
            return;

        if (woundable.ParentWoundable != null)
        {
            Log.Error("Changing WoundableRoot entities is not supported!");
            return;
        }
        woundable.ParentWoundable = woundableEntity;
        if (woundableRoot.ChildWoundables.Add(woundableEntity))
        {
            Log.Error("Woundable has already been registered, this should never happen!");
        }
        Dirty(rootEntity, woundableRoot);
        Dirty(woundableEntity, woundable);
    }

    private void FixWoundableRootsInDetachedPart(BodyPartComponent bodyPart,EntityUid oldRootId,
        WoundableRootComponent oldRoot, EntityUid? origin = null)
    {
        foreach (var (_, bodyPartSlot) in bodyPart.Children)
        {
            if (bodyPartSlot.Child == null)
                continue;
            //setup the woundable root
            var childWoundableRoot = AddComp<WoundableRootComponent>(bodyPartSlot.Child.Value);
            childWoundableRoot.WoundGroupPools = oldRoot.WoundGroupPools;
            childWoundableRoot.WoundTypePools = oldRoot.WoundTypePools;
            //now iterate the children to fix the root references
            foreach (var (childPartEntity, _) in _body.GetPartChildren(bodyPartSlot.Child))
            {
                if (!TryComp<ConditionComponent>(childPartEntity, out var childWoundable))
                    continue;
                childWoundable.ParentWoundable = bodyPartSlot.Child.Value;
                childWoundableRoot.ChildWoundables.Add(childPartEntity);
                foreach (var woundEntity in childWoundable.WoundEntities)
                {
                    var woundDetached = new WoundDetachedEvent(bodyPartSlot.Child.Value, oldRootId,
                        Comp<WoundComponent>(woundEntity), childWoundable, origin);
                    RaiseLocalEvent(woundEntity, ref woundDetached, true);
                }
            }
        }
    }
    private void MergeWoundableRoots(EntityUid oldRootEntity, WoundableRootComponent? oldWoundableRoot,
        EntityUid targetEntity, WoundableRootComponent? newWoundableRoot , EntityUid? origin = null)
    {
        if (!Resolve(oldRootEntity, ref oldWoundableRoot))
            return;
        if (!Resolve(oldRootEntity, ref newWoundableRoot))
        {
            newWoundableRoot = AddComp<WoundableRootComponent>(oldRootEntity);
        }

        foreach (var woundableEntity in oldWoundableRoot.ChildWoundables)
        {
            var woundableComp = Comp<ConditionComponent>(woundableEntity);
            woundableComp.ParentWoundable = targetEntity;
            newWoundableRoot.ChildWoundables.Add(woundableEntity);
            foreach (var woundEntity in woundableComp.WoundEntities)
            {
                var woundAttached = new WoundAttachedEvent(targetEntity, oldRootEntity,
                    Comp<WoundComponent>(woundEntity), woundableComp, origin);
                RaiseLocalEvent(woundEntity, ref woundAttached, true);
            }
        }
        oldWoundableRoot.ChildWoundables.Clear();
        RemComp<WoundableRootComponent>(oldRootEntity);
    }
}
