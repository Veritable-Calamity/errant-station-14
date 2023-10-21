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
            woundable.AccumulatedFrameTime += frameTime;
            //update the next update time before checking if this is allowed to heal to prevent garbage from being
            //constantly ticked by update
            if (woundable.AccumulatedFrameTime  < _healUpdateRate)
                continue;
            woundable.AccumulatedFrameTime -= _healUpdateRate;

            if (!woundable.CanHeal)
                continue;
            var oldHp = woundable.HitPoints;
            woundable.HitPoints = FixedPoint2.Clamp(
                _globalHealMultiplier* (woundable.HitPoints + woundable.HealingMultiplier * woundable.HealingRate),
                0, woundable.HitPointCap);
            UpdateWoundSeverities(uid,woundable);
            if (woundable.HitPoints != oldHp) //only dirty if we actually updated hp
                Dirty(uid, woundable);
        }
    }
}
