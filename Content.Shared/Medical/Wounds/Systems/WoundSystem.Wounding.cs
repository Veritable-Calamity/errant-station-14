using Content.Shared.Damage;
using Content.Shared.Medical.Wounds.Components;
using Robust.Shared.Containers;

namespace Content.Shared.Medical.Wounds.Systems;

public partial class WoundSystem
{
    private const string WoundContainerId = "Wounds";
    private void InitWounding()
    {
        SubscribeLocalEvent<WoundableComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<WoundableComponent, ComponentShutdown>(OnWoundableShutdown);
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnWoundableDamaged);
        SubscribeLocalEvent<WoundableComponent, DamageChangedEvent>(OnWoundableDamaged);
        SubscribeLocalEvent<WoundableComponent, EntInsertedIntoContainerMessage>(OnWoundableInserted);
        SubscribeLocalEvent<WoundableComponent, EntRemovedFromContainerMessage>(OnWoundableRemoved);
        SubscribeLocalEvent<WoundComponent, EntInsertedIntoContainerMessage>(OnWoundInserted);
        SubscribeLocalEvent<WoundComponent, EntRemovedFromContainerMessage>(OnWoundRemoved);
    }

    private void OnWoundableInit(EntityUid entity, WoundableComponent component,  ComponentInit args)
    {
        component.Wounds = _container.EnsureContainer<Container>(entity,WoundContainerId);
        if (component.HitPoints < 0)
            component.HitPoints = component.HitPointCap;
        if (component.Integrity < 0)
            component.Integrity = component.IntegrityCapMax;
        component.IntegrityCap = component.IntegrityCapMax;
        component.HitpointCapMax = component.HitPointCap;
    }

    private void OnWoundableShutdown(EntityUid child, WoundableComponent woundable, ComponentShutdown args)
    {
        if (woundable.ParentWoundable == null)
            return;
        RemoveWoundableFromParent(child, woundable.ParentWoundable.Value, woundable);
    }


    private void OnWoundableDamaged(EntityUid target, WoundableComponent woundable,  DamageChangedEvent args)
    {

    }

    public bool IsWoundableRoot(EntityUid woundableEntity, WoundableComponent? woundable = null)
    {
        return Resolve(woundableEntity, ref woundable) && woundable.WoundableRoot == woundableEntity;
    }

    private void OnWoundRemoved(EntityUid woundEntity, WoundComponent wound, EntRemovedFromContainerMessage args)
    {
        if (wound.OwningWoundable == EntityUid.Invalid)
            return;
        var ev = new WoundRemovedEvent(woundEntity,wound,Comp<WoundableComponent>(wound.OwningWoundable).WoundableRoot);
        RaiseLocalEvent(wound.OwningWoundable, ref ev, true);
        var ev2 = new WoundRemovedEvent(woundEntity,wound,Comp<WoundableComponent>(wound.OwningWoundable).WoundableRoot);
        RaiseLocalEvent(woundEntity, ref ev2);
        wound.OwningWoundable = EntityUid.Invalid;
        if (_net.IsServer)
            Del(woundEntity);
    }

    private void OnWoundInserted(EntityUid woundEntity, WoundComponent wound, EntInsertedIntoContainerMessage args)
    {
        if (wound.OwningWoundable != EntityUid.Invalid)
            return;
        var ev = new WoundAddedEvent(woundEntity,wound,Comp<WoundableComponent>(wound.OwningWoundable).WoundableRoot);
        RaiseLocalEvent(wound.OwningWoundable, ref ev, true);
        var ev2 = new WoundAddedEvent(woundEntity,wound,Comp<WoundableComponent>(wound.OwningWoundable).WoundableRoot);
        RaiseLocalEvent(woundEntity, ref ev2);
    }

    protected bool AddWound(EntityUid woundableEntity, EntityUid woundEntity , WoundableComponent? woundable = null,
        WoundComponent? wound = null)
    {
        if (!Resolve(woundableEntity, ref woundable)
            || !Resolve(woundEntity, ref wound) || !woundable.Wounds.Contains(woundEntity))
            return false;
        wound.OwningWoundable = woundableEntity;
        Dirty(woundableEntity, woundable);
        return woundable.Wounds.Insert(woundEntity);
    }

    protected bool RemoveWound(EntityUid woundEntity, WoundComponent? wound = null)
    {
        if (!Resolve(woundEntity, ref wound)
                                || !TryComp(wound.OwningWoundable, out WoundableComponent? woundable))
            return false;
        return woundable.Wounds.Remove(wound.OwningWoundable);
    }

    #region WoundableLogic

    /// <summary>
    /// Attaches a woundable to a woundable, and updates the woundable hierarchy.
    /// NOTE: This is automatically handled for bodyparts or if you use containers!
    /// </summary>
    /// <param name="parentEntity">parent entity we want to attach to</param>
    /// <param name="childEntity">the child woundable entity</param>
    /// <param name="parentWoundable">parent woundable component</param>
    /// <param name="childWoundable">child woundable component</param>
    /// <returns>true if successful</returns>
    public bool AttachWoundableToParent(EntityUid parentEntity, EntityUid childEntity,
        WoundableComponent? parentWoundable = null, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(parentEntity, ref parentWoundable) || !Resolve(childEntity, ref childWoundable) ||
            childWoundable.ParentWoundable != null || parentWoundable.WoundableRoot == null)
            return false;
        childWoundable.WoundableRoot = parentWoundable.WoundableRoot;
        parentWoundable.ChildWoundables.Add(childEntity);
        //Only add this woundable to the active list on the root if if (childWoundable.AllowWounds)
        {
            var woundableRoot = Comp<WoundableComponent>(parentWoundable.WoundableRoot.Value);
            woundableRoot.ActiveAttachedWoundables ??= new HashSet<EntityUid>();
            woundableRoot.ActiveAttachedWoundables.Add(childEntity);
        }
        return true;
    }

    /// <summary>
    /// Removes a woundable from a woundable, and updates the woundable hierarchy.
    /// NOTE: This is automatically handled for bodyparts or if you use containers!
    /// </summary>
    /// <param name="childEntity">the child woundable we wish to remove</param>
    /// <param name="parentEntity">the entity we are removing the child from</param>
    /// <param name="childWoundable">child woundable component</param>
    /// <returns>true if successful</returns>
    public bool RemoveWoundableFromParent(EntityUid childEntity, EntityUid parentEntity, WoundableComponent? childWoundable = null)
    {
        if (!Resolve(childEntity, ref childWoundable) || childWoundable.ParentWoundable == null || childWoundable.WoundableRoot == null)
            return false;
        var parentWoundable = Comp<WoundableComponent>(parentEntity);
        var rootWoundable = Comp<WoundableComponent>(childWoundable.WoundableRoot.Value);
        childWoundable.ParentWoundable = null;
        var output = false;
        if (rootWoundable.ActiveAttachedWoundables != null)
            output = rootWoundable.ActiveAttachedWoundables.Remove(childEntity);
        return parentWoundable.ChildWoundables.Remove(childEntity) && output;
    }

    private void OnWoundableRemoved(EntityUid child, WoundableComponent childWoundable, EntRemovedFromContainerMessage args)
    {
        if (!TryComp<WoundableComponent>(args.Container.Owner, out var parentWoundable))
            return;
        RemoveWoundableFromParent(child, args.Container.Owner, childWoundable);
        foreach (var woundEntity in childWoundable.Wounds.ContainedEntities)
        {
            var wound = Comp<WoundComponent>(woundEntity);
            var ev = new WoundAddedEvent(woundEntity, wound, parentWoundable.WoundableRoot);
            RaiseLocalEvent(woundEntity, ref ev);
            if (parentWoundable.WoundableRoot == null)
                continue;
            var ev2 = new WoundAddedEvent(woundEntity, wound, parentWoundable.WoundableRoot);
            RaiseLocalEvent(parentWoundable.WoundableRoot.Value, ref ev2, true);
        }
    }

    private void OnWoundableInserted(EntityUid child, WoundableComponent childWoundable, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<WoundableComponent>(args.Container.Owner, out var parentWoundable))
            return;
        AttachWoundableToParent(args.Container.Owner, child, parentWoundable, childWoundable);
        foreach (var woundEntity in childWoundable.Wounds.ContainedEntities)
        {
            var wound = Comp<WoundComponent>(woundEntity);
            var ev = new WoundAddedEvent(woundEntity, wound, childWoundable.WoundableRoot);
            RaiseLocalEvent(woundEntity, ref ev);
            if (childWoundable.WoundableRoot == null)
                continue;
            var ev2 = new WoundAddedEvent(woundEntity, wound, childWoundable.WoundableRoot);
            RaiseLocalEvent(childWoundable.WoundableRoot.Value, ref ev2, true);
        }
    }

    #endregion
}
