using Content.Server.Bible.Components;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Popups;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Bible
{
    [InjectDependencies]
    public sealed partial class BibleSystem : EntitySystem
    {
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private ActionBlockerSystem _blocker = default!;
        [Dependency] private DamageableSystem _damageableSystem = default!;
        [Dependency] private InventorySystem _invSystem = default!;
        [Dependency] private MobStateSystem _mobStateSystem = default!;
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private SharedActionsSystem _actionsSystem = default!;
        [Dependency] private UseDelaySystem _delay = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BibleComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<SummonableComponent, GetVerbsEvent<AlternativeVerb>>(AddSummonVerb);
            SubscribeLocalEvent<SummonableComponent, GetItemActionsEvent>(GetSummonAction);
            SubscribeLocalEvent<SummonableComponent, SummonActionEvent>(OnSummon);
            SubscribeLocalEvent<FamiliarComponent, MobStateChangedEvent>(OnFamiliarDeath);
            SubscribeLocalEvent<FamiliarComponent, GhostRoleSpawnerUsedEvent>(OnSpawned);
        }

        private readonly Queue<EntityUid> _addQueue = new();
        private readonly Queue<EntityUid> _remQueue = new();

        /// <summary>
        /// This handles familiar respawning.
        /// </summary>
        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach(var entity in _addQueue)
            {
                EnsureComp<SummonableRespawningComponent>(entity);
            }
            _addQueue.Clear();

            foreach(var entity in _remQueue)
            {
                RemComp<SummonableRespawningComponent>(entity);
            }
            _remQueue.Clear();

            foreach (var (respawning, summonableComp) in EntityQuery<SummonableRespawningComponent, SummonableComponent>())
            {
                summonableComp.Accumulator += frameTime;
                if (summonableComp.Accumulator < summonableComp.RespawnTime)
                {
                    continue;
                }
                // Clean up the old body
                if (summonableComp.Summon != null)
                {
                    EntityManager.DeleteEntity(summonableComp.Summon.Value);
                    summonableComp.Summon = null;
                }
                summonableComp.AlreadySummoned = false;
                _popupSystem.PopupEntity(Loc.GetString("bible-summon-respawn-ready", ("book", summonableComp.Owner)), summonableComp.Owner, PopupType.Medium);
                SoundSystem.Play("/Audio/Effects/radpulse9.ogg", Filter.Pvs(summonableComp.Owner), summonableComp.Owner, AudioParams.Default.WithVolume(-4f));
                // Clean up the accumulator and respawn tracking component
                summonableComp.Accumulator = 0;
                _remQueue.Enqueue(respawning.Owner);
            }
        }

        private void OnAfterInteract(EntityUid uid, BibleComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            UseDelayComponent? delay = null;

            if (_delay.ActiveDelay(uid, delay))
                return;

            if (args.Target == null || args.Target == args.User || !_mobStateSystem.IsAlive(args.Target.Value))
            {
                return;
            }

            if (!HasComp<BibleUserComponent>(args.User))
            {
                _popupSystem.PopupEntity(Loc.GetString("bible-sizzle"), args.User, args.User);

                SoundSystem.Play(component.SizzleSoundPath.GetSound(), Filter.Pvs(args.User), args.User);
                _damageableSystem.TryChangeDamage(args.User, component.DamageOnUntrainedUse, true, origin: uid);
                _delay.BeginDelay(uid, delay);

                return;
            }

            // This only has a chance to fail if the target is not wearing anything on their head and is not a familiar.
            if (!_invSystem.TryGetSlotEntity(args.Target.Value, "head", out var _) && !HasComp<FamiliarComponent>(args.Target.Value))
            {
                if (_random.Prob(component.FailChance))
                {
                    var othersFailMessage = Loc.GetString(component.LocPrefix + "-heal-fail-others", ("user", Identity.Entity(args.User, EntityManager)),("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                    _popupSystem.PopupEntity(othersFailMessage, args.User, Filter.PvsExcept(args.User), true, PopupType.SmallCaution);

                    var selfFailMessage = Loc.GetString(component.LocPrefix + "-heal-fail-self", ("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                    _popupSystem.PopupEntity(selfFailMessage, args.User, args.User, PopupType.MediumCaution);

                    SoundSystem.Play("/Audio/Effects/hit_kick.ogg", Filter.Pvs(args.Target.Value), args.User);
                    _damageableSystem.TryChangeDamage(args.Target.Value, component.DamageOnFail, true, origin: uid);
                    _delay.BeginDelay(uid, delay);
                    return;
                }
            }

            var damage = _damageableSystem.TryChangeDamage(args.Target.Value, component.Damage, true, origin: uid);

            if (damage == null || damage.Total == 0)
            {
                var othersMessage = Loc.GetString(component.LocPrefix + "-heal-success-none-others", ("user", Identity.Entity(args.User, EntityManager)),("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                _popupSystem.PopupEntity(othersMessage, args.User, Filter.PvsExcept(args.User), true, PopupType.Medium);

                var selfMessage = Loc.GetString(component.LocPrefix + "-heal-success-none-self", ("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                _popupSystem.PopupEntity(selfMessage, args.User, args.User, PopupType.Large);
            }
            else
            {
                var othersMessage = Loc.GetString(component.LocPrefix + "-heal-success-others", ("user", Identity.Entity(args.User, EntityManager)),("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                _popupSystem.PopupEntity(othersMessage, args.User, Filter.PvsExcept(args.User), true, PopupType.Medium);

                var selfMessage = Loc.GetString(component.LocPrefix + "-heal-success-self", ("target", Identity.Entity(args.Target.Value, EntityManager)),("bible", uid));
                _popupSystem.PopupEntity(selfMessage, args.User, args.User, PopupType.Large);
                SoundSystem.Play(component.HealSoundPath.GetSound(), Filter.Pvs(args.Target.Value), args.User);
                _delay.BeginDelay(uid, delay);
            }
        }

        private void AddSummonVerb(EntityUid uid, SummonableComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess || component.AlreadySummoned || component.SpecialItemPrototype == null)
                return;

            if (component.RequiresBibleUser && !HasComp<BibleUserComponent>(args.User))
                return;

            AlternativeVerb verb = new()
            {
                Act = () =>
                {
                    if (!TryComp<TransformComponent>(args.User, out var userXform)) return;

                    AttemptSummon(component, args.User, userXform);
                },
                Text = Loc.GetString("bible-summon-verb"),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }

        private void GetSummonAction(EntityUid uid, SummonableComponent component, GetItemActionsEvent args)
        {
            if (component.AlreadySummoned)
                return;

            args.Actions.Add(component.SummonAction);
        }
        private void OnSummon(EntityUid uid, SummonableComponent component, SummonActionEvent args)
        {
            AttemptSummon(component, args.Performer, Transform(args.Performer));
        }

        /// <summary>
        /// Starts up the respawn stuff when
        /// the chaplain's familiar dies.
        /// </summary>
        private void OnFamiliarDeath(EntityUid uid, FamiliarComponent component, MobStateChangedEvent args)
        {
            if (args.NewMobState != MobState.Dead || component.Source == null)
                return;

            var source = component.Source;
            if (source != null && TryComp<SummonableComponent>(source, out var summonable))
            {
                _addQueue.Enqueue(summonable.Owner);
            }
        }

        /// <summary>
        /// When the familiar spawns, set its source to the bible.
        /// </summary>
        private void OnSpawned(EntityUid uid, FamiliarComponent component, GhostRoleSpawnerUsedEvent args)
        {
            if (!TryComp<SummonableComponent>(Transform(args.Spawner).ParentUid, out var summonable))
                return;

            component.Source = summonable.Owner;
            summonable.Summon = uid;
        }

        private void AttemptSummon(SummonableComponent component, EntityUid user, TransformComponent? position)
        {
            if (component.AlreadySummoned || component.SpecialItemPrototype == null)
                return;
            if (component.RequiresBibleUser && !HasComp<BibleUserComponent>(user))
                return;
            if (!Resolve(user, ref position))
                return;
            if (component.Deleted || Deleted(component.Owner))
                return;
            if (!_blocker.CanInteract(user, component.Owner))
                return;

            // Make this familiar the component's summon
            var familiar = EntityManager.SpawnEntity(component.SpecialItemPrototype, position.Coordinates);
                            component.Summon = familiar;

            // If this is going to use a ghost role mob spawner, attach it to the bible.
            if (HasComp<GhostRoleMobSpawnerComponent>(familiar))
            {
                _popupSystem.PopupEntity(Loc.GetString("bible-summon-requested"), user, PopupType.Medium);
                Transform(familiar).AttachParent(component.Owner);
            }
            component.AlreadySummoned = true;
            _actionsSystem.RemoveAction(user, component.SummonAction);
        }
    }

    public sealed partial class SummonActionEvent : InstantActionEvent
    {

    }
}
