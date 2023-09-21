using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Consciousness;

[ByRefEvent]
public record struct ConsciousnessUpdatedEvent(bool IsConscious, FixedPoint2 ConsciousnessDelta);
