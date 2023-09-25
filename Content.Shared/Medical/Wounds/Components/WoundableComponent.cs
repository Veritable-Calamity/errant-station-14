using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Medical.Wounds.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WoundableComponent : Component //Component that holds wound system configuration data  for a specific entity
{
    [AutoNetworkedField] public EntityUid? ParentWoundable;
    [AutoNetworkedField] public EntityUid RootWoundable;
    [ViewVariables] public HashSet<EntityUid> ChildWoundables = new();
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
    [AutoNetworkedField, DataField("woundPools",
         customTypeSerializer:typeof(PrototypeIdDictionarySerializer<string,DamageTypePrototype>), required:true)]
    public Dictionary<string, string> WoundPools = new();

    [ViewVariables] public Container Wounds;

    [DataField("hitPointsCap", required: true),AutoNetworkedField]
    public FixedPoint2 HitpointCapMax = 90;
    [AutoNetworkedField]public FixedPoint2 HitPointCap = -1;

    [DataField("hitPoints"), AutoNetworkedField] public FixedPoint2 HitPoints = -1;

    [DataField("integrityCap", required: true), AutoNetworkedField]
    public FixedPoint2 IntegrityCapMax = 10;

    [AutoNetworkedField] public FixedPoint2 IntegrityCap = -1;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 Integrity = -1;
    public FixedPoint2 TotalHp => HitPoints + Integrity;
    public FixedPoint2 TotalCap => HitPointCap + IntegrityCap;
    public FixedPoint2 TotalCapMax => HitpointCapMax + IntegrityCapMax;
}
