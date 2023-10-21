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
        component.RootWoundable = entity; //Set root to itself by default!
        component.Wounds = _container.EnsureContainer<Container>(entity,WoundContainerId);
        component.IntegrityCap = component.IntegrityCapMax;
        component.HitPointCap = component.HitPointCapMax;
        if (component.HitPoints < 0)
            component.HitPoints = component.HitPointCapMax;
        if (component.Integrity < 0)
            component.Integrity = component.IntegrityCapMax;
    }

    /// <summary>
    /// Try to spawn the specified wound on a woundable entity
    /// </summary>
    /// <param name="target">target that owns the woundable</param>
    /// <param name="woundProtoId">wound prototype id</param>
    /// <param name="woundData">outputs an entity and wound component pair if successful</param>
    /// <param name="woundable">woundable component</param>
    /// <returns>true if successful</returns>
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

    /// <summary>
    /// Gets the woundpool prototype for a specified type of damage.
    /// </summary>
    /// <param name="target">target that owns the woundable</param>
    /// <param name="damageType">damage type prototype</param>
    /// <param name="woundable">woundable componenet</param>
    /// <returns></returns>
    public ProtoId<WoundPoolPrototype>? GetWoundPoolFromDamageType(EntityUid target, ProtoId<DamageTypePrototype> damageType, WoundableComponent? woundable)
    {
        if (!Resolve(target, ref woundable) || !woundable.AllowWounds)
            return null;
        woundable.WoundPools.TryGetValue(damageType, out var protoId);
        return protoId;
    }

    /// <summary>
    /// Gets a wound from the woundpool based on the amount of damage recieved
    /// </summary>
    /// <param name="woundPoolId"> the woundpool to select a wound from</param>
    /// <param name="damage"> damage being applied</param>
    /// <returns>prototype id of the selected wound or null if no wound should be created</returns>
    public ProtoId<EntityPrototype>? SelectWoundProtoUsingDamage(ProtoId<WoundPoolPrototype> woundPoolId, FixedPoint2 damage)
    {
        var woundPool = _prototype.Index(woundPoolId);
        ProtoId<EntityPrototype>? lastProtoId = null;
        if (damage > woundPool.Wounds.Last().Key)
            return lastProtoId;
        foreach (var (threshold, protoId) in woundPool.Wounds)
        {
            if (threshold > damage)
                return lastProtoId;
            lastProtoId = protoId;
        }
        return lastProtoId;
    }

    /// <summary>
    /// Check if this woundable is root
    /// </summary>
    /// <param name="woundableEntity">Owner of the woundable</param>
    /// <param name="woundable">woundable component</param>
    /// <returns>true if the woundable is the root of the hierarchy</returns>
    public bool IsWoundableRoot(EntityUid woundableEntity, WoundableComponent? woundable = null)
    {
        return Resolve(woundableEntity, ref woundable) && woundable.RootWoundable == woundableEntity;
    }

    //Handler for when a wound is removed from a woundable
    private void OnWoundRemoved(EntityUid woundableEntity, WoundComponent wound, EntRemovedFromContainerMessage args)
    {
        if (wound.ParentWoundable == EntityUid.Invalid)
            return;
        var oldParentWoundable = Comp<WoundableComponent>(wound.ParentWoundable);
        var oldWoundableRoot = Comp<WoundableComponent>(oldParentWoundable.RootWoundable);

        wound.ParentWoundable = EntityUid.Invalid;
        var ev = new WoundRemovedEvent(args.Entity,wound, oldParentWoundable, oldWoundableRoot);
        RaiseLocalEvent(args.Entity, ref ev);
        var ev2 = new WoundRemovedEvent(args.Entity,wound, oldParentWoundable, oldWoundableRoot);
        RaiseLocalEvent(oldParentWoundable.RootWoundable, ref ev2, true);

        if (_net.IsServer)
            Del(woundableEntity);
    }

    //Handler for when a wound is inserted into a woundable
    private void OnWoundInserted(EntityUid woundableEntity, WoundComponent wound, EntInsertedIntoContainerMessage args)
    {
        if (wound.ParentWoundable != EntityUid.Invalid)
            return;
        var parentWoundable = Comp<WoundableComponent>(wound.ParentWoundable);
        var woundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);

        var ev = new WoundAddedEvent(args.Entity,wound,parentWoundable, woundableRoot);
        RaiseLocalEvent(args.Entity, ref ev);
        var ev2 = new WoundAddedEvent(args.Entity,wound,parentWoundable, woundableRoot);
        RaiseLocalEvent(parentWoundable.RootWoundable, ref ev2, true);

    }

    //Internal function for adding a wound to a woundable
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

    //internal function for removing a wound from a woundable
    protected bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound)
                                || !TryComp(wound.ParentWoundable, out WoundableComponent? woundable))
            return false;
        return woundable.Wounds!.Remove(wound.ParentWoundable);
    }

    #region WoundableLogic

    //Function to fix woundable root references in child to prevent references to the old woundable root from persisting on children
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

    //internal implementation to parent a woundable to another
    protected void InternalAddWoundableToParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent parentWoundable, WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Add(childEntity);
        childWoundable.ParentWoundable = parentEntity;
        childWoundable.RootWoundable = parentWoundable.RootWoundable;
        FixWoundableRoots(childEntity, childWoundable);
        var woundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);

        var woundableAttached= new WoundableAttachedEvent(parentEntity, parentWoundable);
        RaiseLocalEvent(childEntity, ref woundableAttached, true);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundAddedEvent(woundId, wound, childWoundable, woundableRoot);
            RaiseLocalEvent(woundId, ref ev);
            var ev2 = new WoundAddedEvent(woundId, wound, childWoundable, woundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
        }
        Dirty(childEntity, childWoundable);
    }

    //internal implementation to remove a woundable from a parent
    protected void InternalRemoveWoundableFromParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent parentWoundable, WoundableComponent childWoundable)
    {
        parentWoundable.ChildWoundables.Remove(childEntity);
        childWoundable.ParentWoundable = null;
        childWoundable.RootWoundable = childEntity;
        FixWoundableRoots(childEntity, childWoundable);
        var oldWoundableRoot = Comp<WoundableComponent>(parentWoundable.RootWoundable);

        var woundableDetached = new WoundableDetachedEvent(parentEntity, parentWoundable);
        RaiseLocalEvent(childEntity, ref woundableDetached, true);

        foreach (var (woundId, wound) in GetAllWounds(childEntity, childWoundable))
        {
            var ev = new WoundRemovedEvent(woundId, wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(woundId, ref ev);
            var ev2 = new WoundRemovedEvent(woundId, wound, childWoundable, oldWoundableRoot);
            RaiseLocalEvent(childWoundable.RootWoundable, ref ev2, true);
        }
        Dirty(childEntity, childWoundable);
    }

    /// <summary>
    /// Removes a woundable from it's parent (if present)
    /// </summary>
    /// <param name="parentEntity">Owner of the parent woundable</param>
    /// <param name="childEntity">Owner of the child woundable</param>
    /// <param name="parentWoundable"></param>
    /// <param name="childWoundable"></param>
    /// <returns>true if successful</returns>
    public bool RemoveWoundableFromParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent? parentWoundable = null, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(parentEntity, ref parentWoundable) || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
            return false;
        InternalRemoveWoundableFromParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }

    /// <summary>
    /// Parents a woundable to another
    /// </summary>
    /// <param name="parentEntity">Owner of the new parent</param>
    /// <param name="childEntity">Owner of the woundable we want to attach</param>
    /// <param name="parentWoundable">The new parent woundable component</param>
    /// <param name="childWoundable">The woundable we are attaching</param>
    /// <returns>true if successful</returns>
    public bool AddWoundableToParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent? parentWoundable = null, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(parentEntity, ref parentWoundable) || !Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null)
            return false;
        InternalAddWoundableToParent(parentEntity, childEntity, parentWoundable, childWoundable);
        return true;
    }

    /// <summary>
    /// Gets all woundable children of a specified woundable
    /// </summary>
    /// <param name="targetEntity">Owner of the woundable</param>
    /// <param name="targetWoundable"></param>
    /// <returns>Enemerable to the found children</returns>
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

    /// <summary>
    /// Finds all children of a specified woundable that have a specific component
    /// </summary>
    /// <param name="targetEntity"></param>
    /// <param name="targetWoundable"></param>
    /// <typeparam name="T">the type of the component we want to find</typeparam>
    /// <returns>Enemerable to the found children</returns>
    public IEnumerable<(EntityUid, WoundableComponent, T)> GetAllWoundableChildrenWithComp<T>(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null) where T: Component, new()
    {
        if (!Resolve(targetEntity, ref targetWoundable))
            yield break;

        foreach (var childEntity in targetWoundable.ChildWoundables)
        {
            if (!TryComp(childEntity, out WoundableComponent? childWoundable))
                continue;
            foreach (var value in GetAllWoundableChildrenWithComp<T>(childEntity, childWoundable))
            {
                yield return value;
            }
        }

        if (!TryComp(targetEntity, out T? foundComp))
            yield break;
        yield return (targetEntity, targetWoundable,foundComp);
    }

    /// <summary>
    /// Get the wounds present on a specific woundable
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component</param>
    /// <returns>An enmerable pointing to one of the found wounds</returns>
    public IEnumerable<(EntityUid, WoundComponent)> GetWoundableWounds(EntityUid targetEntity,
        WoundableComponent? targetWoundable = null)
    {
        if (!Resolve(targetEntity, ref targetWoundable) || targetWoundable.Wounds!.Count == 0)
            yield break;
        foreach (var woundEntity in targetWoundable.Wounds!.ContainedEntities)
        {
            yield return (woundEntity, Comp<WoundComponent>(woundEntity));
        }
    }

    /// <summary>
    /// Get all wounds present in the woundable hierarchy
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component</param>
    /// <returns>An enmerable pointing to one of the found wounds</returns>
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

    /// <summary>
    /// Tries to fetch a random child woundable that allows wounding.
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="childWoundable">Outputs an active woundable if one is found</param>
    /// <param name="targetWoundable"Woundable component></param>
    /// <returns>If successful</returns>
    public bool TryGetRandomActiveChildWoundable(EntityUid targetEntity, [NotNullWhen(true)] out (EntityUid,
        WoundableComponent)? childWoundable, WoundableComponent? targetWoundable = null)
    {
        childWoundable = GetRandomActiveChildWoundable(targetEntity, targetWoundable);
        return childWoundable != null;
    }

    /// <summary>
    /// Gets an active child woundable or null
    /// </summary>
    /// <param name="targetEntity">Entity that owns the woundable</param>
    /// <param name="targetWoundable">Woundable component></param>
    /// <returns>Pair of entityid and woundable componenet or null</returns>
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

    //Handler for when a woundable is removed from it's parent
    private void OnWoundableRemoved(EntityUid parentEntity, WoundableComponent parentWoundable, EntRemovedFromContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;
        InternalRemoveWoundableFromParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    //Handler for when a woundable is inserted into a parent
    private void OnWoundableInserted(EntityUid parentEntity, WoundableComponent parentWoundable, EntInsertedIntoContainerMessage args)
    {
        if (_net.IsClient || !TryComp<WoundableComponent>(args.Entity, out var childWoundable))
            return;
        InternalAddWoundableToParent(parentEntity, args.Entity, parentWoundable, childWoundable);
    }

    #endregion
}
