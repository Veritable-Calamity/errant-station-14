using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared.Medical.Wounds.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConditionComponent : Component //Component that holds wound system configuration data  for a specific entity
{
    [AutoNetworkedField] public EntityUid? ParentWoundable;
    [DataField("allowRootWounds")] public bool AllowRootWounds;
    /// <summary>
    /// How much to scale damage when applying wounds
    /// </summary>
    [AutoNetworkedField,  DataField("damageScaling")]
    public FixedPoint2 DamageScaling = 1;
    //TODO: write validator
    [AutoNetworkedField, DataField("damageGroupPools",
         customTypeSerializer:typeof(PrototypeIdDictionarySerializer<string,DamageTypePrototype>), required:true)]
    public Dictionary<string, string> WoundGroupPools = new();

    [AutoNetworkedField, DataField("damageTypePools",
         customTypeSerializer:typeof(PrototypeIdDictionarySerializer<string,DamageTypePrototype>), required:true)]
    public Dictionary<string, string> WoundTypePools = new();

    [AutoNetworkedField] public HashSet<EntityUid> WoundEntities = new();
    [ViewVariables] public Container WoundContainer;
    [ViewVariables] public Container ChildWoundables;

    [AutoNetworkedField] public FixedPoint2 HitpointCapMax = 0;
    [DataField("hitPointsCap", required: true), AutoNetworkedField]
    public FixedPoint2 HitPointCap = 90;

    [DataField("hitPoints"), AutoNetworkedField] public FixedPoint2 HitPoints = -1;

    [AutoNetworkedField] public FixedPoint2 IntegrityCap = 0;

    [DataField("integrityCap", required: true), AutoNetworkedField]
    public FixedPoint2 IntegrityCapMax = 10;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 Integrity = -1;
    public FixedPoint2 TotalHp => HitPoints + Integrity;
}
