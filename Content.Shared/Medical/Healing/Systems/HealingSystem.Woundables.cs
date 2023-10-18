using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Healing.Systems;

public sealed partial class HealingSystem
{
    private void UpdateWoundables(float frameTime)
    {
        var woundables = EntityQueryEnumerator<WoundableComponent>();

        while (woundables.MoveNext(out var uid, out var woundable))
        {
            if (_timing.CurTime < woundable.NextUpdate)
                continue;
            woundable.NextUpdate += _healUpdateRate;//update the next update time before checking if this is allowed to heal to
                                                   //prevent garbage from being constantly ticked by update
            if (!woundable.CanHeal)
                continue;
            var oldHp = woundable.HitPoints;
            woundable.HitPoints = FixedPoint2.Clamp(woundable.HitPoints+woundable.HealingMultiplier*woundable.HealingRate,
                0, woundable.HitPointCap);
            UpdateWoundSeverities(uid,woundable);
            if (woundable.HitPoints != oldHp) //only dirty if we actually updated hp
                Dirty(uid, woundable);
        }
    }
}
