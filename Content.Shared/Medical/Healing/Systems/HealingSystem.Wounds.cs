using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Healing.Systems;

public sealed partial class HealingSystem
{
    private void UpdateWoundSeverities(EntityUid woundableEntity,WoundableComponent woundable)
    {
        if (woundable.Wounds == null || !woundable.CanHeal)
            return;
        foreach (var woundEntity in woundable.Wounds.ContainedEntities)
        {
            var wound = Comp<WoundComponent>(woundEntity);
            if (!wound.CanHeal)
                continue;
            var oldSeverity = wound.Severity;
            wound.Severity =
                FixedPoint2.Clamp(
                    wound.Severity + wound.BaseHealingRate * wound.HealingMultiplier +
                    wound.HealingModifier, 0, 100);
            var severityDelta = oldSeverity - wound.Severity;
            if (severityDelta == 0)
                continue; //Don't do anything if the severity didn't actually change
            Dirty(woundEntity, wound);
            UpdateCoreStats(woundableEntity, woundable, wound, severityDelta);
            var ev = new WoundSeverityChangedEvent(woundEntity, wound, woundable.RootWoundable, oldSeverity,
                severityDelta);
            RaiseLocalEvent(woundEntity, ref ev);
        }
    }

    private void UpdateCoreStats(EntityUid woundableEntity, WoundableComponent woundable, WoundComponent wound, FixedPoint2 severityDelta)
    {
        if (wound.CanHealInt)
        {
            woundable.IntegrityCap += wound.IntegrityDamage * severityDelta;
        }
        woundable.HitPointCap += wound.HitpointDamage * severityDelta;
        Dirty(woundableEntity, woundable);
    }
}
