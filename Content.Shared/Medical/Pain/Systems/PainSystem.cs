using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Pain.Components;
using Content.Shared.Medical.Wounds;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Medical.Wounds.Systems;

namespace Content.Shared.Medical.Pain.Systems;

public sealed partial class PainSystem : EntitySystem
{

    [Dependency] private readonly WoundSystem _woundSystem = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    public override void Initialize()
    {
        InitializeNerves();
        InitializeDamage();
    }
    /// <summary>
    /// Updates pain value on the nervous system without dirtying the owning entity,
    /// this should only be used to remove unneeded dirty calls in most cases just use ChangePain
    /// </summary>
    /// <param name="targetEntity">Target to add/subtract pain from, this is generally the root woundable entity</param>
    /// <param name="nervousSystem">Nervous system component</param>
    /// <param name="deltaPain">Pain amount to increase or decrease by</param>
    /// <param name="localMultiplier">Value to multiply pain by, usually this a nerve's multiplier</param>
    public void ChangePainNoDirty(EntityUid targetEntity, NervousSystemComponent nervousSystem, FixedPoint2 deltaPain,
        FixedPoint2? localMultiplier = null)
    {
        var oldPain = nervousSystem.Pain;
        localMultiplier ??= 1;
        nervousSystem.RawPain += localMultiplier.Value*deltaPain;
        nervousSystem.RawPain = FixedPoint2.Clamp(nervousSystem.RawPain, 0, nervousSystem.PainCap);

        var painChanged = new PainChangedEvent(targetEntity, nervousSystem, oldPain, nervousSystem.Pain - oldPain);
        RaiseLocalEvent(targetEntity, ref painChanged, true);
    }

    //Internally used implementation of change pain that outputs the nervous system for dirtying
    private bool Internal_ChangePainNoDirty(EntityUid targetEntity, WoundableComponent woundable, FixedPoint2 deltaPain,
        [NotNullWhen(true)] out NervousSystemComponent? nervousSystem, NerveComponent nerve)
    {
        nervousSystem = null;
        if (!TryComp(woundable.RootWoundable, out nervousSystem))
            return false;
        var oldPain = nervousSystem.Pain;
        nervousSystem.RawPain += nerve.PainMultiplier*deltaPain;
        nervousSystem.RawPain = FixedPoint2.Clamp(nervousSystem.RawPain, 0, nervousSystem.PainCap);

        var painChanged = new PainChangedEvent(targetEntity, nervousSystem, oldPain, nervousSystem.Pain - oldPain);
        RaiseLocalEvent(woundable.RootWoundable, ref painChanged, true);
        return true;
    }


    /// <summary>
    /// Updates pain value on the nervous system without dirtying the owning entity,
    /// this should only be used to remove unneeded dirty calls in most cases just use ChangePain
    /// </summary>
    /// <param name="targetEntity">Target woundable entity to add/subtract pain from</param>
    /// <param name="woundable">woundable component that we are applying this pain to</param>
    /// <param name="deltaPain">Pain amount to increase or decrease by</param>
    /// <param name="nerve">the nerve component present on the woundable</param>
    public void ChangePainNoDirty(EntityUid targetEntity, WoundableComponent woundable, FixedPoint2 deltaPain, NerveComponent? nerve)
    {
        if (!Resolve(targetEntity, ref nerve))
            return;
        Internal_ChangePainNoDirty(targetEntity, woundable, deltaPain, out _, nerve);
    }

    /// <summary>
    /// Updates pain value on the nervous system connected to the woundable and dirties the component, this is the prefered way to update pain
    /// </summary>
    /// <param name="targetEntity">Target woundable entity to add/subtract pain from</param>
    /// <param name="woundable">woundable component that we are applying this pain to</param>
    /// <param name="deltaPain">Pain amount to increase or decrease by</param>
    /// <param name="nerve">the nerve component present on the woundable</param>
    public void ChangePain(EntityUid targetEntity, WoundableComponent woundable, FixedPoint2 deltaPain, NerveComponent? nerve = null)
    {
        if (!Resolve(targetEntity, ref nerve) ||!Internal_ChangePainNoDirty(targetEntity, woundable, deltaPain, out var nervousSystem, nerve))
            return;
        Dirty(targetEntity, nervousSystem);
    }

    /// <summary>
    /// Updates pain value on the nervous system and dirties the component, this is the prefered way to update pain
    /// </summary>
    /// <param name="targetEntity">Target to add/subtract pain from, this is generally the root woundable entity</param>
    /// <param name="nervousSystem">Nervous system component</param>
    /// <param name="deltaPain">Pain amount to increase or decrease by</param>
    /// <param name="localMultiplier">Value to multiply pain by, usually this a nerve's multiplier</param>
    public void ChangePain(EntityUid targetEntity, NervousSystemComponent nervousSystem, FixedPoint2 deltaPain,
        FixedPoint2? localMultiplier = null)
    {
        ChangePainNoDirty(targetEntity, nervousSystem, deltaPain, localMultiplier);
        Dirty(targetEntity, nervousSystem);
    }


}
