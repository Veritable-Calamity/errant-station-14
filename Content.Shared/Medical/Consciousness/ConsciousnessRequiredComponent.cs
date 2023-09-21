using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Consciousness;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConsciousnessRequiredComponent : Component
{
    [AutoNetworkedField, DataField("identifier")]
    public string Identifier = "requiredConsciousnessPart";

    //does not having this part force death or only unconsciousness
    [AutoNetworkedField, DataField("causesDeath")]
    public bool CausesDeath = true;
}
