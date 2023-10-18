using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Healing.Systems;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private static readonly TimeSpan HealUpdateRate = new(0,0,1);
    public override void Initialize()
    {

    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateWoundables(frameTime);
    }
}
