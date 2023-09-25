using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Medical.Wounds.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Shared.Medical.Wounds.Systems;

public partial class WoundSystem
{
    private const string WoundContainerId = "Wounds";
    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, EntInsertedIntoContainerMessage>(OnWoundableInserted);
        SubscribeLocalEvent<WoundableComponent, EntRemovedFromContainerMessage>(OnWoundableRemoved);
        SubscribeLocalEvent<WoundComponent, EntInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntRemovedFromContainerMessage>(OnWoundRemoved);
        SubscribeLocalEvent<WoundableComponent, ComponentGetState>(OnWoundableGetState);
        SubscribeLocalEvent<WoundableComponent, ComponentHandleState>(OnWoundableHandleState);
    }

    private void OnWoundableHandleState(EntityUid uid, WoundableComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not WoundableComponentState state)
            return;
        component.HitPoints = state.HitPoints;
        component.HitPointCap = state.HitPointCap;
        component.HitPointCapMax = state.HitPointCapMax;
        component.Integrity = state.Integrity;
        component.IntegrityCap = state.IntegrityCap;
        component.IntegrityCapMax = state.IntegrityCapMax;
        component.Wounds = _container.EnsureContainer<Container>(uid, state.Wounds);
        component.AllowWounds = state.AllowWounds;
        component.DamageScaling = state.DamageScaling;
        component.RootWoundable = GetEntity(state.RootWoundable);
        component.ParentWoundable = GetEntity(state.ParentWoundable);
        component.ChildWoundables.Clear();
        foreach (var netEntity in state.ChildWoundables)
        {
            component.ChildWoundables.Add(GetEntity(netEntity));
        }
    }

    private void OnWoundableGetState(EntityUid uid, WoundableComponent component, ref ComponentGetState args)
    {
        var state = new WoundableComponentState
        {
            HitPoints = component.HitPoints,
            HitPointCap = component.HitPointCap,
            HitPointCapMax = component.HitPointCapMax,
            Integrity = component.Integrity,
            IntegrityCap = component.IntegrityCap,
            IntegrityCapMax = component.IntegrityCapMax,
            Wounds = component.Wounds!.ID,
            AllowWounds = component.AllowWounds,
            DamageScaling = component.DamageScaling,
            RootWoundable = GetNetEntity(component.RootWoundable),
            ParentWoundable = GetNetEntity(component.ParentWoundable),
            ChildWoundables = new HashSet<NetEntity>()
        };
        foreach (var entity in component.ChildWoundables)
        {
            state.ChildWoundables.Add(GetNetEntity(entity));
        }
    }

    private void OnWoundableInit(EntityUid entity, WoundableComponent component,  ComponentInit args)
    {
        component.Wounds = _container.EnsureContainer<Container>(entity,WoundContainerId);
        if (component.HitPoints < 0)
            component.HitPoints = component.HitPointCapMax;
        if (component.Integrity < 0)
            component.Integrity = component.IntegrityCapMax;
        component.IntegrityCap = component.IntegrityCapMax;
        component.HitPointCap = component.HitPointCapMax;
    }

    public bool TrySpawnWound(EntityUid target, ProtoId<EntityPrototype> woundProtoId, out (EntityUid, WoundComponent) woundData,
        WoundableComponent? woundable = null)
    {
        woundData = default;
        if (!Resolve(target, ref woundable) || !_net.IsServer) //This should not run on the client
            return false;

        var woundEntity = Spawn(woundProtoId);
        _transform.SetParent(woundEntity, target);
        if (!TryComp<WoundComponent>(woundEntity, out var woundComp))
        {
            Del(woundEntity);
            Log.Error($"Tried to create wound from entity prototype without a wound component: {woundProtoId}");
            return false;
        }
        if (!AddWound(target, woundEntity, woundable, woundComp))
        {
            Log.Error($"something went wrong adding wound {woundEntity} to woundable {target}");
            return false;
        }
        woundData = (woundEntity, woundComp);
#if DEBUG
        var woundMeta = Comp<MetaDataComponent>(woundEntity);
        Log.Verbose($"Wound: {woundEntity} of type: {woundMeta.EntityPrototype?.Name} Created on {target}");
#endif
        return true;
    }



    public string? GetWoundPoolFromDamageType(EntityUid target, ProtoId<DamageTypePrototype> damageType, WoundableComponent? woundable)
    {
        if (!Resolve(target, ref woundable) || !woundable.AllowWounds)
            return null;
        woundable.WoundPools.TryGetValue(damageType, out var protoId);
        return protoId;
    }

    public ProtoId<EntityPrototype>? GetWoundProtoFromDamage(ProtoId<WoundPoolPrototype> woundPoolId, FixedPoint2 percentDamage)
    {
        var woundPool = _prototype.Index(woundPoolId);
        ProtoId<EntityPrototype>? lastProtoId = null;
        if (percentDamage > 100)
            return lastProtoId;
        foreach (var (threshold, protoId) in woundPool.Wounds)
        {
            if (threshold > percentDamage)
                return lastProtoId;
            lastProtoId = protoId;
        }
        return lastProtoId;
    }

    public bool IsWoundableRoot(EntityUid woundableEntity, WoundableComponent? woundable = null)
    {
        return Resolve(woundableEntity, ref woundable) && woundable.RootWoundable == woundableEntity;
    }

    private void OnWoundRemoved(EntityUid woundableEntity, WoundComponent wound, EntRemovedFromContainerMessage args)
    {
        if (wound.ParentWoundable == EntityUid.Invalid)
            return;
        var rootWoundable = Comp<WoundableComponent>(wound.ParentWoundable).RootWoundable;
        wound.ParentWoundable = EntityUid.Invalid;
        var ev = new WoundRemovedEvent(args.Entity,wound,rootWoundable);
        RaiseLocalEvent(args.Entity, ref ev);
        var ev2 = new WoundRemovedEvent(args.Entity,wound,rootWoundable);
        RaiseLocalEvent(rootWoundable, ref ev2, true);

        if (_net.IsServer)
            Del(woundableEntity);
    }

    private void OnWoundInserted(EntityUid woundableEntity, WoundComponent wound, EntInsertedIntoContainerMessage args)
    {
        if (wound.ParentWoundable != EntityUid.Invalid)
            return;
        var rootWoundable = Comp<WoundableComponent>(wound.ParentWoundable).RootWoundable;
        var ev = new WoundAddedEvent(args.Entity,wound,rootWoundable);
        RaiseLocalEvent(args.Entity, ref ev);
        var ev2 = new WoundAddedEvent(args.Entity,wound,rootWoundable);
        RaiseLocalEvent(rootWoundable, ref ev2, true);

    }

    protected bool AddWound(EntityUid woundableEntity, EntityUid woundEntity , WoundableComponent? woundable = null,
        WoundComponent? wound = null)
    {
        if (!Resolve(woundableEntity, ref woundable)
            || !Resolve(woundEntity, ref wound) || woundable.Wounds!.Contains(woundEntity))
            return false;
        wound.ParentWoundable = woundableEntity;
        Dirty(woundableEntity, woundable);
        return woundable.Wounds.Insert(woundEntity);
    }

    protected bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound)
                                || !TryComp(wound.ParentWoundable, out WoundableComponent? woundable))
            return false;
        return woundable.Wounds!.Remove(wound.ParentWoundable);
    }

    #region WoundableLogic

    private void FixWoundableRoots(EntityUid targetEntity, WoundableComponent targetWoundable)
    {
        if (targetWoundable.ChildWoundables.Count == 0)
            return;
        foreach (var (childEntity, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            childWoundable.RootWoundable = targetWoundable.RootWoundable;
            Dirty(childEntity, childWoundable);
        }
        Dirty(targetEntity, targetWoundable);
    }

    protected void InternalAddWoundableToParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent parentWoundable, WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Add(childEntity);
        childWoundable.ParentWoundable = parentEntity;
        childWoundable.RootWoundable = parentWoundable.RootWoundable;
        FixWoundableRoots(childEntity, childWoundable);
        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(woundId, wound, childWoundable.RootWoundable);
            RaiseLocalEvent(woundId, ref ev);
            var ev2 = new WoundAddedEvent(woundId, wound, childWoundable.RootWoundable);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
        }
        Dirty(childEntity, childWoundable);
    }

    protected void InternalRemoveWoundableFromParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent parentWoundable, WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Remove(childEntity);
        childWoundable.ParentWoundable = null;
        childWoundable.RootWoundable = childEntity;
        FixWoundableRoots(childEntity, childWoundable);
        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundRemovedEvent(woundId, wound, childWoundable.RootWoundable);
            RaiseLocalEvent(woundId, ref ev);
            var ev2 = new WoundRemovedEvent(woundId, wound, childWoundable.RootWoundable);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
        }
        Dirty(childEntity, childWoundable);

    }

    public bool RemoveWoundableFromParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent? parentWoundable = null, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(parentEntity, ref parentWoundable) || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
            return false;
        InternalRemoveWoundableFromParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }

    public bool AddWoundableToParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent? parentWoundable = null, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(parentEntity, ref parentWoundable) || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
            return false;
        InternalAddWoundableToParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }

    public IEnumerable<(EntityUid, WoundableComponent)> GetAllWoundableChildren(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable))
            yield break;

        foreach (var childEntity in targetWoundable.ChildWoundables)
        {
            if (!TryComp(childEntity, out WoundableComponent? childWoundable))
                continue;
            foreach (var value in GetAllWoundableChildren(childEntity, childWoundable))
            {
                yield return value;
            }
        }
        yield return (targetEntity, targetWoundable);
    }

    public IEnumerable<(EntityUid, WoundComponent)> GetAllWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable) || targetWoundable.Wounds!.Count == 0)
            yield break;
        foreach (var (_, childWoundable) in GetAllWoundableChildren(targetEntity, targetWoundable))
        {
            foreach (var woundEntity in childWoundable.Wounds!.ContainedEntities)
            {
                yield return (woundEntity, Comp<WoundComponent>(woundEntity));
            }
        }
    }


    public bool TryGetRandomActiveChildWoundable(EntityUid targetEntity, [NotNullWhen(true)] out (EntityUid,
        WoundableComponent)? childWoundable, WoundableComponent? targetWoundable = null)
    {
        childWoundable = GetRandomActiveChildWoundable(targetEntity, targetWoundable);
        return childWoundable != null;
    }

    public (EntityUid, WoundableComponent)? GetRandomActiveChildWoundable(EntityUid targetEntity, WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable))
            return null;
        (EntityUid, WoundableComponent) testVal = (targetEntity, targetWoundable);

        if (targetWoundable.ChildWoundables.Count == 0)
        {
            if (targetWoundable.AllowWounds)
                return testVal;
            return null;
        }

        const int attemptLimit = 20;
        const float settleChance = 0.3f;
        var attempts = 0;
        while (attempts <= attemptLimit)
        {
            attempts++;
            if (testVal.Item2.AllowWounds && _random.NextDouble() < settleChance || testVal.Item2.ChildWoundables.Count == 0)
            {
                break;
            }
            var foundEntity = testVal.Item2.ChildWoundables.ElementAt(_random.Next(0, testVal.Item2.ChildWoundables.Count));
            testVal = (foundEntity, Comp<WoundableComponent>(foundEntity));
        }
        return testVal;
    }

    private void OnWoundableRemoved(EntityUid parentEntity, WoundableComponent parentWoundable, EntRemovedFromContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;
        InternalRemoveWoundableFromParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    private void OnWoundableInserted(EntityUid parentEntity, WoundableComponent parentWoundable, EntInsertedIntoContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;
        InternalAddWoundableToParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    #endregion
}
