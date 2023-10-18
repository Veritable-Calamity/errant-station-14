using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Healing.Systems;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private static TimeSpan _healUpdateRate;
    public override void Initialize()
    {
        SetupCVars();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateWoundables(frameTime);
    }

    private void SetupCVars()
    {
        _healUpdateRate = new TimeSpan(0,0,_config.GetCVar<int>("medical.healing_rate"));
        _config.OnValueChanged<int>("medical.healing_rate",
            newRate => { _healUpdateRate = new TimeSpan(0, 0, newRate); });
    }
}
