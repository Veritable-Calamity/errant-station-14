using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Pain.Components;

[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NerveComponent : Component
{
    [DataField("painMultiplier"), AutoNetworkedField]
    public FixedPoint2 PainMultiplier = 1.0f;
}
