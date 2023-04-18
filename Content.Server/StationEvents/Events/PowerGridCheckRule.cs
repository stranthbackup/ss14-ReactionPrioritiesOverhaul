using Content.Server.Power.Components;
using JetBrains.Annotations;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System.Threading;
using Content.Server.Power.EntitySystems;
using Timer = Robust.Shared.Timing.Timer;
using System.Linq;
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Random;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;

namespace Content.Server.StationEvents.Events
{
    [UsedImplicitly]
    public sealed class PowerGridCheckRule : StationEventSystem<PowerGridCheckRuleComponent>
    {
        [Dependency] private readonly ApcSystem _apcSystem = default!;

        protected override void Added(EntityUid uid, PowerGridCheckRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
        {
            base.Added(uid, component, gameRule, args);

            component.EndAfter = RobustRandom.Next(60, 120);
        }

        protected override void Started(EntityUid uid, PowerGridCheckRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
        {
            base.Started(uid, component, gameRule, args);

            if (StationSystem.Stations.Count == 0)
                return;
            var chosenStation = RobustRandom.Pick(StationSystem.Stations.ToList());

            foreach (var (apc, transform) in EntityQuery<ApcComponent, TransformComponent>(true))
            {
                if (apc.MainBreakerEnabled && CompOrNull<StationMemberComponent>(transform.GridUid)?.Station == chosenStation)
                    component.Powered.Add(apc.Owner);
            }

            RobustRandom.Shuffle(component.Powered);

            component.NumberPerSecond = Math.Max(1, (int)(component.Powered.Count / component.SecondsUntilOff)); // Number of APCs to turn off every second. At least one.
        }

        protected override void Ended(EntityUid uid, PowerGridCheckRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
        {
            base.Ended(uid, component, gameRule, args);

            foreach (var entity in component.Unpowered)
            {
                if (EntityManager.Deleted(entity)) continue;

                if (EntityManager.TryGetComponent(entity, out ApcComponent? apcComponent))
                {
                    if(!apcComponent.MainBreakerEnabled)
                        _apcSystem.ApcToggleBreaker(entity, apcComponent);
                }
            }

            // Can't use the default EndAudio
            component.AnnounceCancelToken?.Cancel();
            component.AnnounceCancelToken = new CancellationTokenSource();
            Timer.Spawn(3000, () =>
            {
                Audio.PlayGlobal("/Audio/Announcements/power_on.ogg", Filter.Broadcast(), true, AudioParams.Default.WithVolume(-4f));
            }, component.AnnounceCancelToken.Token);
            component.Unpowered.Clear();
        }

        protected override void RuleTick(EntityUid uid, PowerGridCheckRuleComponent component, GameRuleComponent gameRule, float frameTime)
        {
            base.RuleTick(uid, component, gameRule, frameTime);

            //todo figure out this bs
            /*
            if (Elapsed > _endAfter)
            {
                ForceEndSelf();
                return;
            }*/

            var updates = 0;
            component.FrameTimeAccumulator += frameTime;
            if (component.FrameTimeAccumulator > component.UpdateRate)
            {
                updates = (int) (component.FrameTimeAccumulator / component.UpdateRate);
                component.FrameTimeAccumulator -= component.UpdateRate * updates;
            }

            for (var i = 0; i < updates; i++)
            {
                if (component.Powered.Count == 0)
                    break;

                var selected = component.Powered.Pop();
                if (Deleted(selected))
                    continue;
                if (TryComp<ApcComponent>(selected, out var apcComponent))
                {
                    if (apcComponent.MainBreakerEnabled)
                        _apcSystem.ApcToggleBreaker(selected, apcComponent);
                }
                component.Unpowered.Add(selected);
            }
        }
    }
}
