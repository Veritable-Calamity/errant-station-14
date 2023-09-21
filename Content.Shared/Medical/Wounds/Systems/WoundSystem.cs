using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Medical.Wounds.Systems;

public sealed partial class WoundSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    private const string WoundContainerId = "WoundContainer";
    private const string ChildWoundableContainerId = "WoundContainer";
    public override void Initialize()
    {
        SubscribeLocalEvent<ConditionComponent, ComponentInit>(OnWoundableInit);
        SubscribeLocalEvent<ConditionComponent, ComponentShutdown>(OnWoundableShutdown);
        SubscribeLocalEvent<ConditionComponent, DamageChangedEvent>(OnWoundableDamaged);
        SubscribeLocalEvent<ConditionComponent, RejuvenateEvent>(OnWoundableRejuvenate);
    }

    private void OnWoundableInit(EntityUid entity, ConditionComponent component,  ComponentInit args)
    {
        component.WoundContainer = _container.EnsureContainer<Container>(entity,WoundContainerId);
        component.ChildWoundables = _container.EnsureContainer<Container>(entity, ChildWoundableContainerId);
        if (component.HitPoints < 0)
            component.HitPoints = component.HitPointCap;
        if (component.Integrity < 0)
            component.Integrity = component.IntegrityCapMax;
        component.IntegrityCap = component.IntegrityCapMax;
        component.HitpointCapMax = component.HitPointCap;
    }

    private void OnWoundableShutdown(EntityUid entity, ConditionComponent component, ComponentShutdown args)
    {
    }

    //This will relay damage to a randomly selected woundable, this is going to be replaced by targeting.
    private void OnWoundableRelayDamageToRandom(EntityUid target, ConditionComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

    }



    private void OnWoundableRejuvenate(EntityUid uid, ConditionComponent component, RejuvenateEvent args)
    {

    }

    private void OnWoundableDamaged(EntityUid target, ConditionComponent condition,  DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
        {
            Log.Warning($"Damage was SET on {condition} this will result in unexpected behaviour! " +
                        $"Do not use set damage on woundable entities!");
            return;
        }

        if (condition.ParentWoundable == null)
        {
            if (!condition.AllowRootWounds)
            {
                //TODO: Randomly select a part from the child parts to apply damage to
                var childWoundableArray = condition.ChildWoundables.ContainedEntities.ToArray();

                if (childWoundableArray.Length == 0)
                    return; //do not relay if there are no children
                var randomChildWoundable = childWoundableArray[_random.Next(0, childWoundableArray.Length-1)];
                _damageable.TryChangeDamage(randomChildWoundable, args.DamageDelta, false, true,
                    args.Damageable, args.Origin);
            }
            else
            {

            }
        }

        OnDamageApplied(target, condition, args.DamageIncreased, args.DamageDelta.Total.Value, args.Origin,
            args.DamageDelta);
    }
}
