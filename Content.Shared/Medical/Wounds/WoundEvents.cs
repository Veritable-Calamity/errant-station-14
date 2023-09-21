using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Wounds;

[ByRefEvent]
public record struct TryApplyWoundEvent(EntityUid Target, ConditionComponent ConditionComponent, bool ShouldApply,
    EntityUid? Origin);

[ByRefEvent]
public record struct WoundDetachedEvent(EntityUid NewRootEntity, EntityUid OldRootEntity, WoundComponent WoundComponent,
    ConditionComponent ConditionComponent, EntityUid? Origin);

[ByRefEvent]
public record struct WoundAttachedEvent(EntityUid NewRootEntity, EntityUid OldRootEntity, WoundComponent WoundComponent,
    ConditionComponent ConditionComponent, EntityUid? Origin);

[ByRefEvent]
public record struct WoundAppliedEvent(EntityUid WoundEntity, WoundComponent WoundComponent,  EntityUid? Origin);

[ByRefEvent]
public record struct WoundClearedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, EntityUid? Origin);
