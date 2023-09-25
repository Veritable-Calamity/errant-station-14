using System.Collections;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Body.Components;
using Content.Shared.Medical.Wounds.Components;
using Content.Shared.Medical.Wounds.Systems;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Toolshed.TypeParsers;

namespace Content.Server.Medical.Wounds.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class WoundsCommand : ToolshedCommand
{
    private WoundSystem? _woundSystem;

    [CommandImplementation("list_wounds")]
    public IEnumerable<WoundWrap> ListWounds([CommandInvocationContext] IInvocationContext ctx
    )
    {
        _woundSystem ??= GetSys<WoundSystem>();
        if (ExecutingEntity(ctx) is { } ent)
            return ListWoundsImplementation(ent, _woundSystem);
        if (ctx.Session is { } session)
            ctx.ReportError(new SessionHasNoEntityError(session));
        else
            ctx.ReportError(new NotForServerConsoleError());
        return new List<WoundWrap>();
    }

    [CommandImplementation("list_wounds")]
    public IEnumerable<WoundWrap> ListWounds([PipedArgument] EntityUid target
    )
    {
        _woundSystem ??= GetSys<WoundSystem>();
        return ListWoundsImplementation(target, _woundSystem);
    }

    private IEnumerable<WoundWrap> ListWoundsImplementation(EntityUid target, WoundSystem woundSystem)
    {
        List<WoundWrap> woundEntities = new();
        if (!TryGetRootWoundableFromBody(target, out var data))
            return woundEntities;


        foreach (var (woundEntity, woundComp) in woundSystem.GetAllWounds(data.Item1,data.Item2))
        {
            woundEntities.Add(new WoundWrap(woundEntity, EntityManager, woundComp));
        }
        return woundEntities;
    }

    public record struct WoundWrap(EntityUid WoundEntity, IEntityManager EntityManager, WoundComponent WoundComp) : IToolshedPrettyPrint, IAsType<EntityUid>
    {
        public string PrettyPrint(ToolshedManager toolshed, out IEnumerable? more, bool moreUsed = false, int? maxOutput = null)
        {
            more = null;
            return $"{EntityManager.ToPrettyString(WoundEntity)}: \n" +
                   $"parent: {EntityManager.ToPrettyString(WoundComp.ParentWoundable)} " +
                   $"type: {EntityManager.GetComponent<MetaDataComponent>(WoundEntity).EntityPrototype} " +
                   $"sev: {WoundComp.Severity} \n===";
        }

        public EntityUid AsType()
        {
            return WoundEntity;
        }
    }


    [CommandImplementation("list_woundables")]
    public IEnumerable<WoundableWrap> ListWoundables([CommandInvocationContext] IInvocationContext ctx
    )
    {
        _woundSystem ??= GetSys<WoundSystem>();
        if (ExecutingEntity(ctx) is { } ent)
            return ListWoundablesImplementation(ent, _woundSystem);
        if (ctx.Session is { } session)
            ctx.ReportError(new SessionHasNoEntityError(session));
        else
            ctx.ReportError(new NotForServerConsoleError());
        return new List<WoundableWrap>();
    }

    [CommandImplementation("list_woundables")]
    public IEnumerable<WoundableWrap> ListWoundables([PipedArgument] EntityUid target
    )
    {
        _woundSystem ??= GetSys<WoundSystem>();
        return ListWoundablesImplementation(target, _woundSystem);
    }

    private IEnumerable<WoundableWrap> ListWoundablesImplementation(EntityUid target, WoundSystem woundSystem)
    {
        var woundableEntities = new List<WoundableWrap>();
        if (!TryGetRootWoundableFromBody(target, out var data))
            return woundableEntities;
        woundableEntities.Add(new WoundableWrap(data.Item1, EntityManager, data.Item2));
        foreach (var (woundableId, woundable) in woundSystem.GetAllWoundableChildren(data.Item1, data.Item2))
        {
            woundableEntities.Add(new WoundableWrap(woundableId, EntityManager, woundable));
        }
        return woundableEntities;
    }


    private bool TryGetRootWoundableFromBody(EntityUid target, out (EntityUid,WoundableComponent) rootData)
    {
        rootData = default;
        if (!TryComp<BodyComponent>(target, out var body)
            || body.RootContainer.ContainedEntity == null
            || !TryComp<WoundableComponent>(body.RootContainer.ContainedEntity, out var woundableRoot))
            return false;
        rootData = (body.RootContainer.ContainedEntity.Value, woundableRoot);
        return true;
    }

    public record struct WoundableWrap(EntityUid WoundableEntity, IEntityManager EntityManager, WoundableComponent Woundable) : IToolshedPrettyPrint, IAsType<EntityUid>
    {
        public string PrettyPrint(ToolshedManager toolshed, out IEnumerable? more, bool moreUsed = false, int? maxOutput = null)
        {
            more = null;
           return $"== Woundable {EntityManager.ToPrettyString(WoundableEntity)}: " +
                $"hp: {Woundable.HitPoints}/{Woundable.HitPointCap}:{Woundable.HitPointCapMax} " +
                $"int: {Woundable.Integrity}/{Woundable.IntegrityCap}:{Woundable.IntegrityCapMax} ==";
        }

        public EntityUid AsType()
        {
            return WoundableEntity;
        }
    }


}
