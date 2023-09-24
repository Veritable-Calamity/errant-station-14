using Content.Shared.Body.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Wounds;

[ByRefEvent]
public record struct TryApplyWoundEvent(EntityUid Target, WoundableComponent WoundableComponent, bool ShouldApply,
    EntityUid? Origin);

[ByRefEvent]
public record struct WoundAddedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, EntityUid? RootWoundableEntity);

[ByRefEvent]
public record struct WoundRemovedEvent(EntityUid WoundEntity, WoundComponent WoundComponent, EntityUid? RootWoundableEntity);
