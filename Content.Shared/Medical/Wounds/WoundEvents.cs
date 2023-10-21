using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Wounds;

[ByRefEvent]
public record struct TryApplyWoundEvent(EntityUid Target, WoundableComponent WoundableComponent, bool ShouldApply,
    EntityUid? Origin);

[ByRefEvent]
public record struct WoundAddedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, WoundableComponent Woundable, WoundableComponent RootWoundable);

[ByRefEvent]
public record struct WoundRemovedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, WoundableComponent OldWoundable, WoundableComponent OldRootWoundable);

[ByRefEvent]
public record struct WoundableAttachedEvent(EntityUid ParentWoundableEntity, WoundableComponent ParentWoundableComponent);

[ByRefEvent]
public record struct WoundableDetachedEvent(EntityUid ParentWoundableEntity, WoundableComponent ParentWoundableComponent);



[ByRefEvent]
public record struct WoundSeverityChangedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, WoundableComponent Woundable,
    FixedPoint2 OldSeverity, FixedPoint2 SeverityDelta);
