using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Medical.Wounds.Components;


[RegisterComponent, NetworkedComponent]
public sealed partial class WoundableComponent : Component //Component that holds wound system configuration data  for a specific entity
{
    [ViewVariables, AutoNetworkedField] public EntityUid? ParentWoundable;
    [ViewVariables, AutoNetworkedField] public EntityUid RootWoundable;
    [ViewVariables, AutoNetworkedField] public HashSet<EntityUid> ChildWoundables = new();
    /// <summary>
    /// Should we allow wounds to be created on this woundable. This is usually set to false on root woundables ie: body entity.
    /// This is also useful if you want to have an unwoundable part between woundable parts to relay damage across!
    /// If this is set, all damage checks/hp values are ignored and the values are passed on to child woundables (or ignored!)
    /// </summary>
    [DataField("allowWounds")] public bool AllowWounds = true;
    /// <summary>
    /// How much to scale damage when applying wounds
    /// </summary>
    [AutoNetworkedField,  DataField("damageScaling")]
    public FixedPoint2 DamageScaling = 1;

    //TODO Resistances!

    //TODO: write validator

    /// <summary>
    /// WoundPools for damage types
    /// </summary>
    [DataField("woundPools",
         customTypeSerializer:typeof(PrototypeIdDictionarySerializer<string,DamageTypePrototype>), required:true)]
    public Dictionary<string, string> WoundPools = new();

    [ViewVariables] public Container? Wounds;

    [DataField("hpCap", required: true)]public FixedPoint2 HitPointCapMax;
    public FixedPoint2 HitPointCap = -1;

    [DataField("hp")] public FixedPoint2 HitPoints = -1;

    [DataField("intCap", required: true)]public FixedPoint2 IntegrityCapMax;

    public FixedPoint2 IntegrityCap = -1;

    public TimeSpan NextUpdate;

    [DataField("int")] public FixedPoint2 Integrity = -1;

    public WoundableComponent(Container wounds)
    {
        Wounds = wounds;
    }

    public FixedPoint2 TotalHp => HitPoints + Integrity;
    public FixedPoint2 TotalCap => HitPointCap + IntegrityCap;
    public FixedPoint2 TotalCapMax => HitPointCapMax + IntegrityCapMax;
}

[Serializable, NetSerializable]
public sealed class WoundableComponentState : ComponentState
{
    public NetEntity? ParentWoundable = default!;
    public NetEntity RootWoundable = default!;
    public HashSet<NetEntity> ChildWoundables = default!;
    public FixedPoint2 DamageScaling = default!;
    public Dictionary<string, string> WoundPools = default!;
    public string Wounds = default!;
    public bool AllowWounds = default!;
    public FixedPoint2 HitPointCapMax = default!;
    public FixedPoint2 HitPointCap = default!;
    public FixedPoint2 HitPoints = default!;
    public FixedPoint2 IntegrityCapMax = default!;
    public FixedPoint2 IntegrityCap = default!;
    public FixedPoint2 Integrity = default!;
}
