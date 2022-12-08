using Content.Server.CombatMode;
using Content.Server.NPC.Components;
using Content.Server.NPC.Events;
using Content.Shared.NPC;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCCombatSystem
{
    private const float TargetMeleeLostRange = 14f;

    private void InitializeMelee()
    {
        SubscribeLocalEvent<NPCMeleeCombatComponent, ComponentStartup>(OnMeleeStartup);
        SubscribeLocalEvent<NPCMeleeCombatComponent, ComponentShutdown>(OnMeleeShutdown);
        SubscribeLocalEvent<NPCMeleeCombatComponent, NPCSteeringEvent>(OnMeleeSteering);
    }

    private void OnMeleeSteering(EntityUid uid, NPCMeleeCombatComponent component, ref NPCSteeringEvent args)
    {
        if (false && TryComp<MeleeWeaponComponent>(component.Weapon, out var weapon))
        {
            var cdRemaining = weapon.NextAttack - _timing.CurTime;

            if (cdRemaining < TimeSpan.FromSeconds(1f / weapon.AttackRate) * 0.25f)
                return;

            // If CD remaining then backup.
            if (!_physics.TryGetNearestPoints(uid, component.Target, out _, out var pointB))
            {
                return;
            }

            var obstacleDirection = args.OffsetRotation.RotateVec(pointB - args.WorldPosition);
            var obstacleDistance = obstacleDirection.Length;

            if (obstacleDistance == 0f)
                return;

            var norm = obstacleDirection.Normalized;
            var idealDistance = weapon.Range * 0.75f;

            var weight = (obstacleDistance <= args.AgentRadius
                ? 1f
                : (idealDistance - obstacleDistance) / idealDistance);

            for (var i = 0; i < SharedNPCSteeringSystem.InterestDirections; i++)
            {
                var result = Vector2.Dot(norm, args.Directions[i]) * weight * 0.2f;

                if (result < 0f)
                    continue;

                args.DangerMap[i] = MathF.Max(args.DangerMap[i], result);
            }

            for (var i = 0; i < SharedNPCSteeringSystem.InterestDirections; i++)
            {
                var result = -Vector2.Dot(norm, args.Directions[i]) * weight;

                if (result < 0f)
                    continue;

                args.InterestMap[i] = result;
            }
        }
    }

    private void OnMeleeShutdown(EntityUid uid, NPCMeleeCombatComponent component, ComponentShutdown args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode))
        {
            combatMode.IsInCombatMode = false;
        }

        _steering.Unregister(component.Owner);
    }

    private void OnMeleeStartup(EntityUid uid, NPCMeleeCombatComponent component, ComponentStartup args)
    {
        if (TryComp<CombatModeComponent>(uid, out var combatMode))
        {
            combatMode.IsInCombatMode = true;
        }

        // TODO: Cleanup later, just looking for parity for now.
        component.Weapon = uid;
    }

    private void UpdateMelee(float frameTime)
    {
        var combatQuery = GetEntityQuery<CombatModeComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        foreach (var (comp, _) in EntityQuery<NPCMeleeCombatComponent, ActiveNPCComponent>())
        {
            if (!combatQuery.TryGetComponent(comp.Owner, out var combat) || !combat.IsInCombatMode)
            {
                RemComp<NPCMeleeCombatComponent>(comp.Owner);
                continue;
            }

            Attack(comp, xformQuery);
        }
    }

    private void Attack(NPCMeleeCombatComponent component, EntityQuery<TransformComponent> xformQuery)
    {
        component.Status = CombatStatus.Normal;

        // TODO:
        // Also need some blackboard data for stuff like juke frequency, assigning target slots (to surround targets), etc.
        // miss %
        if (!TryComp<MeleeWeaponComponent>(component.Weapon, out var weapon))
        {
            component.Status = CombatStatus.NoWeapon;
            return;
        }

        if (!xformQuery.TryGetComponent(component.Owner, out var xform) ||
            !xformQuery.TryGetComponent(component.Target, out var targetXform))
        {
            component.Status = CombatStatus.TargetUnreachable;
            return;
        }

        if (!xform.Coordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance))
        {
            component.Status = CombatStatus.TargetUnreachable;
            return;
        }

        if (distance > TargetMeleeLostRange)
        {
            component.Status = CombatStatus.TargetUnreachable;
            return;
        }

        if (TryComp<NPCSteeringComponent>(component.Owner, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            component.Status = CombatStatus.TargetUnreachable;
            return;
        }

        if (distance > weapon.Range)
        {
            component.Status = CombatStatus.TargetOutOfRange;
            return;
        }

        steering = EnsureComp<NPCSteeringComponent>(component.Owner);
        steering.Range = MathF.Max(0.2f, weapon.Range - 0.4f);

        // Gets unregistered on component shutdown.
        _steering.TryRegister(component.Owner, new EntityCoordinates(component.Target, Vector2.Zero), steering);

        if (Enabled)
            _melee.AttemptLightAttack(component.Owner, weapon, component.Target);
    }
}
