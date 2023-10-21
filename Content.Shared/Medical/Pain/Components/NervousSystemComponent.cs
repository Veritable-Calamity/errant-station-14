using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Pain.Components;

[NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NervousSystemComponent : Component
{
    [DataField("rawPain"), AutoNetworkedField]
    public FixedPoint2 RawPain = 0f;

    [DataField("painCap"), AutoNetworkedField]
    public FixedPoint2 PainCap = 100f;

    [DataField("painModifier"), AutoNetworkedField]
    public FixedPoint2 PainModifier = 1.0f;

    [AutoNetworkedField]
    public HashSet<EntityUid> AttachedReceivers = new();

    //TODO: pain thresholds here?

    //this is updated by painDamage and exists for esoteric effects like wizard or pain-rays
    [AutoNetworkedField]
    public FixedPoint2 GenericPain;

    public FixedPoint2 Pain => RawPain + GenericPain * PainModifier;
}
