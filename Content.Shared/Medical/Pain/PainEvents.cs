using Content.Shared.FixedPoint;
using Content.Shared.Medical.Pain.Components;

namespace Content.Shared.Medical.Pain;

[ByRefEvent]
public record struct PainChangedEvent(EntityUid OriginatorEntity, NervousSystemComponent NervousSystem, FixedPoint2 OldPain, FixedPoint2 PainDelta);

//Do we need a modifier changed event? Probably not, so I won't bother adding it unless someone needs it for something
