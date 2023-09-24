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
}
