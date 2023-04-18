using Content.Server.GameTicking.Rules.Components;
using Content.Server.Radio;
using Robust.Shared.Random;
using Content.Server.Light.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;

namespace Content.Server.StationEvents.Events;

public sealed class SolarFlareRule : StationEventSystem<SolarFlareRuleComponent>
{
    [Dependency] private readonly PoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedDoorSystem _door = default!;

    private float _effectTimer = 0;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioSendAttempt);
    }

    protected override void Started(EntityUid uid, SolarFlareRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        //todo WHY  GOD
        //_event.EndAfter = RobustRandom.Next(ev.MinEndAfter, ev.MaxEndAfter);
    }

    protected override void RuleTick(EntityUid uid, SolarFlareRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.RuleTick(uid, component, gameRule, frameTime);

        _effectTimer -= frameTime;
        if (_effectTimer < 0)
        {
            _effectTimer += 1;
            var lightQuery = EntityQueryEnumerator<PoweredLightComponent>();
            while (lightQuery.MoveNext(out var lightEnt, out var light))
            {
                if (RobustRandom.Prob(component.LightBreakChancePerSecond))
                    _poweredLight.TryDestroyBulb(lightEnt, light);
            }
            var airlockQuery = EntityQueryEnumerator<AirlockComponent, DoorComponent>();
            while (airlockQuery.MoveNext(out var airlockEnt, out var airlock, out var door))
            {
                if (airlock.AutoClose && RobustRandom.Prob(component.DoorToggleChancePerSecond))
                    _door.TryToggleDoor(airlockEnt, door);
            }
        }

        //todo this is bs
        /*
        if (Elapsed > _event.EndAfter)
        {
            ForceEndSelf();
            return;
        }*/
    }

    private void OnRadioSendAttempt(ref RadioReceiveAttemptEvent args)
    {
        var query = EntityQueryEnumerator<SolarFlareRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var flare, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            if (!flare.AffectedChannels.Contains(args.Channel.ID))
                continue;

            if (!flare.OnlyJamHeadsets || (HasComp<HeadsetComponent>(args.RadioReceiver) || HasComp<HeadsetComponent>(args.RadioSource)))
                args.Cancelled = true;
        }
    }
}
