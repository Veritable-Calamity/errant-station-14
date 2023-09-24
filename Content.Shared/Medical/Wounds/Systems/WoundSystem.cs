using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Rejuvenate;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.Medical.Wounds.Systems;

[Virtual]
public partial class WoundSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<WoundableComponent, RejuvenateEvent>(OnWoundableRejuvenate);
        InitWounding();
        InitDamage();
    }


    //This will relay damage to a randomly selected woundable, this is going to be replaced by targeting.
    private void OnWoundableRelayDamageToRandom(EntityUid target, WoundableComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta == null)
            return;

    }

    private void OnWoundableRejuvenate(EntityUid uid, WoundableComponent component, RejuvenateEvent args)
    {

    }
}
