using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.Medical.Healing.Systems;

public sealed partial class HealingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private static float _healUpdateRate;
    private static float _globalHealMultiplier;
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
        _healUpdateRate = 1/_config.GetCVar(CCVars.MedicalHealingTickrate);
        _globalHealMultiplier = _config.GetCVar(CCVars.MedicalHealingMultiplier);

        _config.OnValueChanged(CCVars.MedicalHealingTickrate,
            newRate => { _healUpdateRate = 1 / newRate;});
        _config.OnValueChanged(CCVars.MedicalHealingMultiplier,
            newMult => { _globalHealMultiplier = newMult;});
    }
}
