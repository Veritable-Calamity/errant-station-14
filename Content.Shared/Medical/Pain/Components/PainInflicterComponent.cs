using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Pain.Components;

[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PainInflicterComponent : Component
{
    [DataField("pain", required: true), AutoNetworkedField]
    public FixedPoint2 Pain;
}
