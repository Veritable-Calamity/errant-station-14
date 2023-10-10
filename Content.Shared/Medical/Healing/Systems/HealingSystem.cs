using Content.Shared.Medical.Wounds.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Healing.Systems;

public sealed class HealingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan HealUpdateRate = new(0,0,1);

    private EntityQueryEnumerator<WoundableComponent> woundables;
    public override void Initialize()
    {
        woundables = EntityQueryEnumerator<WoundableComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        while (woundables.MoveNext(out var uid, out var woundable))
        {
            if (_timing.CurTime < woundable.NextUpdate)
                continue;
            woundable.NextUpdate += HealUpdateRate;

        }
    }
}
