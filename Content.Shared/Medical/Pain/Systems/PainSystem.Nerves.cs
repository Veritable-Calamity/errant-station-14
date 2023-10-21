using Content.Shared.FixedPoint;
using Content.Shared.Medical.Pain.Components;
using Content.Shared.Medical.Wounds;
using Content.Shared.Medical.Wounds.Components;

namespace Content.Shared.Medical.Pain.Systems;

public sealed partial class PainSystem
{
    private void InitializeNerves()
    {
        SubscribeLocalEvent<PainInflicterComponent, WoundAddedEvent>(OnWoundAdded);
        SubscribeLocalEvent<PainInflicterComponent, WoundRemovedEvent>(OnWoundRemoved);
        SubscribeLocalEvent<PainInflicterComponent, WoundSeverityChangedEvent>(OnWoundSeverityChanged);
    }

    /// <summary>
    /// Sets the pain multiplier on a nerve component. This properly raises pain events
    /// </summary>
    /// <param name="nerveEntity">Entity that owns the nerve comp</param>
    /// <param name="newMultiplier">New pain multiplier</param>
    /// <param name="nerve">nerve component</param>
    public void SetNervePainMultiplier(EntityUid nerveEntity, FixedPoint2 newMultiplier,
        NerveComponent? nerve = null)
    {
        if (!Resolve(nerveEntity, ref nerve)
            || !TryComp<WoundableComponent>(nerveEntity, out var woundable)
            || !TryComp<NervousSystemComponent>(woundable.RootWoundable, out var nervousSystem))
            return;
        var oldMult = nerve.PainMultiplier;
        nerve.PainMultiplier = newMultiplier;
        var systemUpdated = false;
        var oldPain = nervousSystem.Pain;
        foreach (var (woundEntity, wound) in _woundSystem.GetWoundableWounds(nerveEntity))
        {
            if (!TryComp<PainInflicterComponent>(woundEntity, out var painInflicter))
                continue;
            systemUpdated = true;

            nervousSystem.RawPain += wound.Severity * (newMultiplier - oldMult) * painInflicter.Pain;
        }

        if (systemUpdated)
        {
            var painChanged = new PainChangedEvent(nerveEntity, nervousSystem, oldPain, nervousSystem.Pain-oldPain);
            RaiseLocalEvent(woundable.RootWoundable ,ref painChanged, true);
            Dirty(woundable.RootWoundable, nervousSystem);
        }
        Dirty(nerveEntity, nerve);
    }

    //Handler for when wound severity is updated to properly adjust pain
    private void OnWoundSeverityChanged(EntityUid uid, PainInflicterComponent painInflicter, ref WoundSeverityChangedEvent args)
    {
        if (!TryComp<NerveComponent>(args.WoundComponent.ParentWoundable, out var nerve))
            return;
        ChangePain(args.Woundable.RootWoundable, args.Woundable, args.SeverityDelta*painInflicter.Pain, nerve);
    }

    //Handler for when a wound is removed to remove its pain from the nervous system
    private void OnWoundRemoved(EntityUid uid, PainInflicterComponent inflicter, ref WoundRemovedEvent args)
    {
        if (!TryComp<NerveComponent>(args.WoundComponent.ParentWoundable, out var nerve))
            return;
        ChangePain(uid, args.OldWoundable, inflicter.Pain, nerve);
    }

    //Handler for when a wound is added to add its pain to the nervous system
    private void OnWoundAdded(EntityUid uid, PainInflicterComponent inflicter, ref WoundAddedEvent args)
    {
        if (!TryComp<NerveComponent>(args.WoundComponent.ParentWoundable, out var nerve))
            return;
        ChangePain(uid, args.Woundable, inflicter.Pain, nerve);
    }
}
