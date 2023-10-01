using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Consciousness;

public sealed class ConsciousnessSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    private const string UnspecifiedIdentifier = "Unspecified";

    public override void Initialize()
    {
        SubscribeLocalEvent<ConsciousnessComponent, MapInitEvent>(OnConsciousnessMapInit);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, ComponentInit>(OnConsciousnessPartInit);
        SubscribeLocalEvent<ConsciousnessRequiredComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ConsciousnessComponent, ComponentGetState>(OnComponentGet);
        SubscribeLocalEvent<ConsciousnessComponent, ComponentHandleState>(OnComponentHandleState);
    }

    private void OnComponentHandleState(EntityUid uid, ConsciousnessComponent component, ref ComponentHandleState args)
    {
        var state = args.Current as ConsciousnessComponentState;
        if (state == null)
            return;
        component.Threshold = state.Threshold;
        component.RawConsciousness = state.RawConsciousness;
        component.Multiplier = state.Multiplier;
        component.Cap = state.Cap;
        component.ForceDead = state.ForceDead;
        component.ForceUnconscious = state.ForceUnconscious;
        component.IsConscious = state.IsConscious;
        component.Modifiers.Clear();
        component.Multipliers.Clear();
        component.RequiredConsciousnessParts.Clear();
        foreach (var ((modEntity, modType), modifier) in state.Modifiers)
        {
            component.Modifiers.Add((EntityManager.GetEntity(modEntity),modType),modifier);
        }
        foreach (var ((multEntity, multType), modifier) in state.Multipliers)
        {
            component.Multipliers.Add((EntityManager.GetEntity(multEntity),multType),modifier);
        }
        foreach (var (id, (entity, causesDeath)) in state.RequiredConsciousnessParts)
        {
            component.RequiredConsciousnessParts.Add(id, (EntityManager.GetEntity(entity), causesDeath));
        }
    }

    private void OnComponentGet(EntityUid uid, ConsciousnessComponent comp, ref ComponentGetState args)
    {
        var state = new ConsciousnessComponentState();
        state.Threshold = comp.Threshold;
        state.RawConsciousness = comp.RawConsciousness;
        state.Multiplier = comp.Multiplier;
        state.Cap = comp.Cap;
        state.ForceDead = comp.ForceDead;
        state.ForceUnconscious = comp.ForceUnconscious;
        state.IsConscious = comp.IsConscious;
        foreach (var ((modEntity, modType), modifier) in comp.Modifiers)
        {
            state.Modifiers.Add((EntityManager.GetNetEntity(modEntity),modType),modifier);
        }
        foreach (var ((multEntity, multType), modifier) in comp.Multipliers)
        {
            state.Multipliers.Add((EntityManager.GetNetEntity(multEntity),multType),modifier);
        }

        foreach (var (id, (entity, causesDeath)) in comp.RequiredConsciousnessParts)
        {
            state.RequiredConsciousnessParts.Add(id, (EntityManager.GetNetEntity(entity), causesDeath));
        }
        args.State = state;
    }

    private void OnBodyPartAdded(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartAddedEvent args)
    {
        if (args.Part.Body == null ||
            !TryComp<ConsciousnessComponent>(args.Part.Body, out var consciousness))
            return;
        if (!consciousness.RequiredConsciousnessParts.ContainsKey(component.Identifier)
            && consciousness.RequiredConsciousnessParts[component.Identifier].Item1 !=null)
        {
            Log.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {args.Part.Body} this will result in unexpected behaviour!");
        }
        consciousness.RequiredConsciousnessParts[component.Identifier] = (uid,component.CausesDeath);
        CheckRequiredParts(args.Part.Body.Value, consciousness);
    }

    private void OnConsciousnessPartInit(EntityUid uid, ConsciousnessRequiredComponent component, ComponentInit args)
    {
        EntityUid? bodyId = null;
        if (TryComp<BodyPartComponent>(uid, out var bodyPart) && bodyPart.Body != null)
        {
            bodyId = bodyPart.Body;
        }
        else  if (TryComp<OrganComponent>(uid, out var organ) && organ.Body != null)
        {
            bodyId = organ.Body;
        }
        if (bodyId == null || !TryComp<ConsciousnessComponent>(bodyId, out var consciousness))
            return;

        if (!consciousness.RequiredConsciousnessParts.TryAdd(component.Identifier, (uid,component.CausesDeath)))
        {
            Log.Warning($"ConsciousnessRequirementPart with duplicate Identifier {component.Identifier}:{uid} added to a body:" +
                        $" {uid} this will result in unexpected behaviour!");
        }
    }

    private void OnOrganRemoved(EntityUid uid, ConsciousnessRequiredComponent component, OrganRemovedFromBodyEvent args)
    {
        if (!TryComp<ConsciousnessComponent>(args.OldBody, out var consciousness))
            return;
        if (!consciousness.RequiredConsciousnessParts.ContainsKey(component.Identifier))
        {
            Log.Warning($"ConsciousnessRequirementPart with indentifier {component.Identifier} not found on body:{uid}");
            return;
        }
        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (null,consciousness.RequiredConsciousnessParts[component.Identifier].Item2);
        CheckRequiredParts(args.OldBody, consciousness);
    }

    private void OnBodyPartRemoved(EntityUid uid, ConsciousnessRequiredComponent component, ref BodyPartRemovedEvent args)
    {
        if (args.Part.Body == null || !TryComp<ConsciousnessComponent>(args.Part.Body.Value, out var consciousness))
            return;
        if (!consciousness.RequiredConsciousnessParts.ContainsKey(component.Identifier))
        {
            Log.Warning($"ConsciousnessRequirementPart with indentifier {component.Identifier} not found on body:{uid}");
            return;
        }
        consciousness.RequiredConsciousnessParts[component.Identifier] =
            (null,consciousness.RequiredConsciousnessParts[component.Identifier].Item2);
        CheckRequiredParts(args.Part.Body.Value, consciousness);
    }

    private void OnConsciousnessMapInit(EntityUid uid, ConsciousnessComponent consciousness, MapInitEvent args)
    {
        //set the starting consciousness to the cap if it is set to auto
        if (consciousness.RawConsciousness < 0)
        {
            consciousness.RawConsciousness = consciousness.Cap;
            Dirty(consciousness);
        }

        CheckConscious(uid, consciousness);
    }

    /// <summary>
    /// Add a unique consciousness modifier. This value gets added to the raw consciousness value.
    /// The owner and type combo must be unique, if you are adding multiple values from a single owner and type, combine them into one modifier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifier">Value of the modifier</param>
    /// <param name="consciousness">ConsciousnessComponent</param>
    /// <param name="identifier">Localized text name for the modifier (for debug/admins)</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool AddConsciousnessModifier(EntityUid target, EntityUid modifierOwner, FixedPoint2 modifier,
        ConsciousnessComponent? consciousness = null, string identifier = UnspecifiedIdentifier, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || modifier == 0)
            return false;

        if (!consciousness.Modifiers.TryAdd((modifierOwner, type), new ConsciousnessModifier(modifier, identifier)))
            return false;

        consciousness.RawConsciousness += modifier;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness), modifier * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(consciousness);
        CheckConscious(target, consciousness);
        return true;
    }


    /// <summary>
    /// Get a copy of a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifier">copy of the found modifier, changes are NOT saved</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool TryGetConsciousnessModifier(EntityUid target, EntityUid modifierOwner,
        out ConsciousnessModifier? modifier,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        modifier = null;
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var rawModifier))
            return false;
        modifier = rawModifier;
        return true;
    }

    /// <summary>
    /// Remove a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool RemoveConsciousnessModifer(EntityUid target, EntityUid modifierOwner,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness))
            return false;
        if (!consciousness.Modifiers.Remove((modifierOwner,type), out var foundModifier))
            return false;
        consciousness.RawConsciousness = -foundModifier.Change;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness),
            foundModifier.Change * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(consciousness);
        CheckConscious(target, consciousness);
        return true;
    }

    /// <summary>
    /// Edit a consciousness modifier. This value gets added to the raw consciousness value.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="modifierChange">Value that is being added onto the modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool EditConsciousnessModifier(EntityUid target, EntityUid modifierOwner, FixedPoint2 modifierChange,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var oldModifier))
            return false;
        var newModifier = oldModifier with {Change = oldModifier.Change + modifierChange};
        consciousness.Modifiers[(modifierOwner,type)] = newModifier;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness),
            modifierChange * consciousness.Multiplier);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(consciousness);
        CheckConscious(target, consciousness);
        return true;
    }

    /// <summary>
    /// Update the identifier string for a consciousness modifier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="modifierOwner">Owner of a modifier</param>
    /// <param name="newIdentifier">New localized string to identify this modifier</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Modifier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool UpdateConsciousnessModifierMetaData(EntityUid target, EntityUid modifierOwner, string newIdentifier,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Modifiers.TryGetValue((modifierOwner,type), out var oldMultiplier))
            return false;
        var newMultiplier = oldMultiplier with {Identifier = newIdentifier};
        consciousness.Modifiers[(modifierOwner, type)] = newMultiplier;
        //TODO: create/raise an identifier changed event if needed
        Dirty(consciousness);
        //we don't need to check consciousness here since no simulation values get changed
        return true;
    }


    /// <summary>
    /// Add a unique consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// The owner and type combo must be unique, if you are adding multiple values from a single owner and type, combine them into one multiplier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplier">Value of the multiplier</param>
    /// <param name="consciousness">ConsciousnessComponent</param>
    /// <param name="identifier">Localized text name for the multiplier (for debug/admins)</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool AddConsciousnessMultiplier(EntityUid target, EntityUid multiplierOwner, FixedPoint2 multiplier,
        ConsciousnessComponent? consciousness = null, string identifier = UnspecifiedIdentifier, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) || multiplier == 0)
            return false;

        if (!consciousness.Multipliers.TryAdd((multiplierOwner,type), new ConsciousnessMultiplier(multiplier, identifier)))
            return false;
        var oldMultiplier = consciousness.Multiplier;
        consciousness.Multiplier += multiplier;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness),
            multiplier * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(target, consciousness);
        CheckConscious(target, consciousness);
        return true;
    }

    /// <summary>
    /// Get a copy of a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplier">Copy of the found multiplier, changes are NOT saved</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <returns>Successful</returns>
    public bool TryGetConsciousnessMultiplier(EntityUid target, EntityUid multiplierOwner,
        out ConsciousnessMultiplier? multiplier, ConsciousnessComponent? consciousness = null,
        ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        multiplier = null;
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var rawMultiplier))
            return false;
        multiplier = rawMultiplier;
        return true;
    }

    /// <summary>
    /// Remove a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    public bool RemoveConsciousnessMultiplier(EntityUid target, EntityUid multiplierOwner,
        ConsciousnessModType type = ConsciousnessModType.Generic,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return false;
        if (!consciousness.Multipliers.Remove((multiplierOwner, type), out var foundMultiplier))
            return false;
        consciousness.Multiplier = -foundMultiplier.Change;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness),
            foundMultiplier.Change * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(target, consciousness);
        CheckConscious(target, consciousness);
        return true;
    }

    /// <summary>
    /// Edit a consciousness multiplier. This value gets added onto the multiplier used to calculate consciousness.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="multiplierChange">Value that is being added onto the multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Successful</returns>
    public bool EditConsciousnessMultiplier(EntityUid target, EntityUid multiplierOwner, FixedPoint2 multiplierChange,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var oldMultiplier))
            return false;
        var newMultiplier = oldMultiplier with {Change = oldMultiplier.Change + multiplierChange};
        consciousness.Multipliers[(multiplierOwner, type)] = newMultiplier;
        var ev = new ConsciousnessUpdatedEvent(IsConscious(target, consciousness),
            multiplierChange * consciousness.RawConsciousness);
        RaiseLocalEvent(target, ref ev, true);
        Dirty(target, consciousness);
        CheckConscious(target, consciousness);
        return true;
    }

    /// <summary>
    /// Update the identifier string for a consciousness multiplier
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="multiplierOwner">Owner of a multiplier</param>
    /// <param name="newIdentifier">New localized string to identify this multiplier</param>
    /// <param name="type">Multiplier type, defaults to generic</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>Sucessful</returns>
    public bool UpdateConsciousnessMultiplierMetaData(EntityUid target, EntityUid multiplierOwner, string newIdentifier,
        ConsciousnessComponent? consciousness = null, ConsciousnessModType type = ConsciousnessModType.Generic)
    {
        if (!Resolve(target, ref consciousness) ||
            !consciousness.Multipliers.TryGetValue((multiplierOwner, type), out var oldMultiplier))
            return false;
        var newMultiplier = oldMultiplier with {Identifier = newIdentifier};
        consciousness.Multipliers[(multiplierOwner, type)] = newMultiplier;
        //TODO: create/raise an identifier changed event if needed
        Dirty(target, consciousness);
        //we don't need to check consciousness here since no simulation values get changed
        return true;
    }

    /// <summary>
    /// Checks to see if an entity should be made unconscious, this is called whenever any consciousness values are changed.
    /// Unless you are directly modifying a consciousness component (pls dont) you don't need to call this.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="consciousness">Consciousness component</param>
    public void CheckConscious(EntityUid target, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            return;
        SetConscious(target, consciousness.Consciousness > consciousness.Threshold, consciousness);
        UpdateMobState(target, consciousness);
    }

    /// <summary>
    /// Gets the current consciousness state of an entity. This is mainly used internally.
    /// </summary>
    /// <param name="target">Target entity</param>
    /// <param name="consciousness">Consciousness component</param>
    /// <returns>True if conscious</returns>
    public bool IsConscious(EntityUid target, ConsciousnessComponent? consciousness = null)
    {
        return Resolve(target, ref consciousness) && consciousness.Consciousness > consciousness.Threshold;
    }

    /// <summary>
    /// Get all consciousness multipliers present on an entity. Note: these are copies, do not try to edit the values
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="consciousness">consciousness component</param>
    /// <returns>Enumerable of Modifiers</returns>
    public IEnumerable<((EntityUid,ConsciousnessModType), ConsciousnessModifier)> GetAllModifiers(EntityUid target,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            yield break;
        foreach (var (owner, modifier) in consciousness.Modifiers)
        {
            yield return (owner, modifier);
        }
    }

    /// <summary>
    /// Get all consciousness multipliers present on an entity. Note: these are copies, do not try to edit the values
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="consciousness">consciousness component</param>
    /// <returns>Enumerable of Multipliers</returns>
    public IEnumerable<((EntityUid,ConsciousnessModType), ConsciousnessMultiplier)> GetAllMultipliers(EntityUid target,
        ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target, ref consciousness))
            yield break;
        foreach (var (owner, multiplier) in consciousness.Multipliers)
        {
            yield return (owner, multiplier);
        }
    }

    /// <summary>
    /// Only used internally. Do not use this, instead use consciousness modifiers/multipliers!
    /// </summary>
    /// <param name="target">target entity</param>
    /// <param name="isConscious">should this entity be conscious</param>
    /// <param name="isAlive">is this entity alive</param>
    /// <param name="consciousness">consciousness component</param>
    /// <param name="mobState">mobState component</param>
    private void SetConscious(EntityUid target, bool isConscious, ConsciousnessComponent? consciousness = null)
    {
        if (!Resolve(target,ref consciousness) || consciousness.IsConscious == isConscious)
            return;
        consciousness.IsConscious = isConscious;
        Dirty(target, consciousness);
    }

    private void UpdateMobState(EntityUid target, ConsciousnessComponent? consciousness = null, MobStateComponent? mobState = null)
    {
        if (!Resolve(target,ref consciousness, ref mobState))
            return;
        var newMobState = consciousness.IsConscious ? MobState.Alive : MobState.Critical;
        if (consciousness.ForceUnconscious)
            newMobState = MobState.Critical;
        if (consciousness.Consciousness <= 0)
            newMobState = MobState.Dead;
        if (consciousness.ForceDead)
            newMobState = MobState.Dead;
        _mobStateSystem.ChangeMobState(target, newMobState, mobState);
    }

    private void CheckRequiredParts(EntityUid bodyId, ConsciousnessComponent consciousness)
    {
        var alive = true;
        var conscious = true;
        foreach (var (identifier, (entity,forcesDeath)) in consciousness.RequiredConsciousnessParts)
        {
            if (entity != null)
                continue;
            if (forcesDeath)
            {
                consciousness.ForceDead = true;
                Dirty(bodyId, consciousness);
                alive = false;
                break;
            }
            conscious = false;
        }
        if (alive)
        {
            consciousness.ForceDead = false;
            consciousness.ForceUnconscious = !conscious;
            Dirty(bodyId, consciousness);
        }
        CheckConscious(bodyId, consciousness);
    }
}
