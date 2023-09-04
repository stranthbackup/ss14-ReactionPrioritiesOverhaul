﻿using System.Linq;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Resist;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Access.Components;
using Content.Shared.Coordinates;

namespace Content.Server.StationEvents.Events;

[InjectDependencies]
public sealed partial class BluespaceLockerRule : StationEventSystem<BluespaceLockerRuleComponent>
{
    [Dependency] private BluespaceLockerSystem _bluespaceLocker = default!;

    protected override void Started(EntityUid uid, BluespaceLockerRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var targets = EntityQuery<EntityStorageComponent, ResistLockerComponent>().ToList();
        RobustRandom.Shuffle(targets);

        foreach (var target in targets)
        {
            var potentialLink = target.Item1.Owner;

            if (HasComp<AccessReaderComponent>(potentialLink) ||
                HasComp<BluespaceLockerComponent>(potentialLink) ||
                !HasComp<StationMemberComponent>(potentialLink.ToCoordinates().GetGridUid(EntityManager)))
                continue;

            var comp = AddComp<BluespaceLockerComponent>(potentialLink);

            comp.PickLinksFromSameMap = true;
            comp.MinBluespaceLinks = 1;
            comp.BehaviorProperties.BluespaceEffectOnTeleportSource = true;
            comp.AutoLinksBidirectional = true;
            comp.AutoLinksUseProperties = true;
            comp.AutoLinkProperties.BluespaceEffectOnInit = true;
            comp.AutoLinkProperties.BluespaceEffectOnTeleportSource = true;
            _bluespaceLocker.GetTarget(potentialLink, comp, true);
            _bluespaceLocker.BluespaceEffect(potentialLink, comp, comp, true);

            Sawmill.Info($"Converted {ToPrettyString(potentialLink)} to bluespace locker");

            return;
        }
    }
}
