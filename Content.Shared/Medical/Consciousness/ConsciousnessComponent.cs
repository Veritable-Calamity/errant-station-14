using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Medical.Consciousness;

[RegisterComponent, NetworkedComponent]
public sealed partial class ConsciousnessComponent : Component
{
    /// <summary>
    /// Unconsciousness threshold, ie: when does this entity pass out
    /// </summary>
    [DataField("threshold", required: true)]
    public FixedPoint2 Threshold = 30;

    /// <summary>
    /// The raw/starting consciousness value, this should be between 0 and the cap or -1 to automatically get the cap
    /// Do not directly edit this value, use modifiers instead!
    /// </summary>
    [DataField("consciousness")]
    public FixedPoint2 RawConsciousness = -1;

    //The current consciousness value adjusted by the multiplier and clamped
    public FixedPoint2 Consciousness => FixedPoint2.Clamp(RawConsciousness * Multiplier, 0, Cap);

    //Multiplies the consciousness value whenever it is used. Do not directly edit this value, use multipliers instead!
    [DataField("multiplier")]
    public FixedPoint2 Multiplier = 1.0;

    //The maximum consciousness value, and starting consciousness if rawConsciousness is -1
    [DataField("cap")] public FixedPoint2 Cap = 100;

    //List of modifiers that are applied to this consciousness
    public Dictionary<(EntityUid, ConsciousnessModType),ConsciousnessModifier> Modifiers = new();

    //List of multipliers that are applied to this consciousness
    public Dictionary<(EntityUid, ConsciousnessModType),ConsciousnessMultiplier> Multipliers = new();

    public Dictionary<string, (EntityUid?, bool)> RequiredConsciousnessParts = new();

    //DO NOT USE THESE!!!! They are used internally for required part tracking!
    public bool ForceDead;
    public bool ForceUnconscious;

    //DO NOT CHANGE THIS! it is used internally for caching!
    [DataField("isConscious")] public bool IsConscious = true;
}

[Serializable, NetSerializable]
public sealed class ConsciousnessComponentState : ComponentState
{
    public FixedPoint2 Threshold;
    public FixedPoint2 RawConsciousness;
    public FixedPoint2 Multiplier;
    public FixedPoint2 Cap;
    public readonly Dictionary<(NetEntity, ConsciousnessModType), ConsciousnessModifier> Modifiers = new();
    public readonly Dictionary<(NetEntity, ConsciousnessModType),ConsciousnessMultiplier> Multipliers = new();
    public readonly Dictionary<string, (NetEntity?, bool)> RequiredConsciousnessParts = new();
    public bool ForceDead;
    public bool ForceUnconscious;
    public bool IsConscious;
}

[Serializable, DataRecord]
public record struct ConsciousnessModifier(FixedPoint2 Change, string Identifier = "Unspecified");

[Serializable, DataRecord]
public record struct ConsciousnessMultiplier(FixedPoint2 Change,
    string Identifier = "Unspecified", ConsciousnessModType Type = ConsciousnessModType.Generic);


