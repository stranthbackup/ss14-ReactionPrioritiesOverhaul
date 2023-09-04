using Content.Shared.ActionBlocker;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement;
using Content.Shared.Movement.Events;
using Content.Shared.Physics.Pull;
using Content.Shared.Pulling;
using Content.Shared.Pulling.Components;
using Content.Shared.Pulling.Events;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;

namespace Content.Shared.Administration;

[InjectDependencies]
public sealed partial class AdminFrozenSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedPullingSystem _pulling = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AdminFrozenComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, InteractionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AdminFrozenComponent, ComponentShutdown>(UpdateCanMove);
        SubscribeLocalEvent<AdminFrozenComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<AdminFrozenComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<AdminFrozenComponent, ChangeDirectionAttemptEvent>(OnAttempt);
    }

    private void OnAttempt(EntityUid uid, AdminFrozenComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnPullAttempt(EntityUid uid, AdminFrozenComponent component, PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnStartup(EntityUid uid, AdminFrozenComponent component, ComponentStartup args)
    {
        if (TryComp<SharedPullableComponent>(uid, out var pullable))
        {
            _pulling.TryStopPull(pullable);
        }

        UpdateCanMove(uid, component, args);
    }

    private void OnUpdateCanMove(EntityUid uid, AdminFrozenComponent component, UpdateCanMoveEvent args)
    {
        if (component.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void UpdateCanMove(EntityUid uid, AdminFrozenComponent component, EntityEventArgs args)
    {
        _blocker.UpdateCanMove(uid);
    }
}
