﻿using Content.Server.PowerCell;
using Content.Shared.Interaction.Events;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.Pinpointer;

/// <summary>
/// This handles logic and interaction relating to <see cref="ProximityBeeperComponent"/>
/// </summary>
public sealed class ProximityBeeperSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ProximityBeeperComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ProximityBeeperComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<ActiveProximityBeeperComponent, PowerCellSlotEmptyEvent>(OnPowerCellSlotEmpty);
    }

    private void OnUseInHand(EntityUid uid, ProximityBeeperComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryToggle(uid, component, args.User);
    }

    private void OnUnpaused(EntityUid uid, ProximityBeeperComponent component, ref EntityUnpausedEvent args)
    {
        component.NextBeepTime += args.PausedTime;
    }

    private void OnPowerCellSlotEmpty(EntityUid uid, ActiveProximityBeeperComponent component, ref PowerCellSlotEmptyEvent args)
    {
        TryDisable(uid, active: component);
    }

    /// <summary>
    /// Beeps the proximitybeeper as well as sets the time for the next beep
    /// based on proximity to entities with the target component.
    /// </summary>
    public void UpdateBeep(EntityUid uid, ProximityBeeperComponent? component = null, ActiveProximityBeeperComponent? active = null, bool playBeep = true)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!Resolve(uid, ref active, false))
        {
            component.NextBeepTime += component.MinBeepInterval;
            return;
        }

        var xform = Transform(uid);
        var xformQuery = GetEntityQuery<TransformComponent>();
        var comp = EntityManager.ComponentFactory.GetRegistration(component.Component).Type;
        float? closestDistance = null;
        foreach (var targetXform in _entityLookup.GetComponentsInRange<TransformComponent>(xform.MapPosition, component.MaximumDistance))
        {
            // forgive me father, for i have sinned.
            var ent = targetXform.Owner;

            if (!HasComp(ent, comp))
                continue;

            var dist = (_transform.GetWorldPosition(xform, xformQuery) - _transform.GetWorldPosition(targetXform, xformQuery)).Length;
            if (dist >= (closestDistance ?? float.MaxValue))
                continue;
            closestDistance = dist;
        }

        if (closestDistance is not { } distance)
            return;

        if (playBeep)
            _audio.PlayPvs(component.BeepSound, uid);

        var scalingFactor = distance / component.MaximumDistance;
        var interval = (component.MaxBeepInterval - component.MinBeepInterval) * scalingFactor + component.MinBeepInterval;
        component.NextBeepTime += interval;
    }

    /// <summary>
    /// Enables the proximity beeper
    /// </summary>
    public bool TryEnable(EntityUid uid, ProximityBeeperComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        TryComp<PowerCellDrawComponent>(uid, out var draw);

        if (!_powerCell.HasActivatableCharge(uid, battery: draw, user: user))
            return false;

        var active = EnsureComp<ActiveProximityBeeperComponent>(uid);
        _appearance.SetData(uid, ProximityBeeperVisuals.Enabled, true);
        component.NextBeepTime = _timing.CurTime;
        UpdateBeep(uid, component, active, false);
        if (draw != null)
            draw.Enabled = true;
        return true;
    }

    /// <summary>
    /// disables the proximity beeper
    /// </summary>
    public bool TryDisable(EntityUid uid, ProximityBeeperComponent? component = null, ActiveProximityBeeperComponent? active = null)
    {
        if (!Resolve(uid, ref component, ref active))
            return false;

        RemComp(uid, active);
        _appearance.SetData(uid, ProximityBeeperVisuals.Enabled, false);
        if (TryComp<PowerCellDrawComponent>(uid, out var draw))
            draw.Enabled = true;
        UpdateBeep(uid, component);
        return true;
    }

    /// <summary>
    /// toggles the proximity beeper
    /// </summary>
    public bool TryToggle(EntityUid uid, ProximityBeeperComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return TryComp<ActiveProximityBeeperComponent>(uid, out var active)
            ? TryDisable(uid, component, active)
            : TryEnable(uid, component, user);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveProximityBeeperComponent, ProximityBeeperComponent>();
        while (query.MoveNext(out var uid, out var active, out var beeper))
        {
            if (_timing.CurTime < beeper.NextBeepTime)
                continue;
            UpdateBeep(uid, beeper, active);
        }
    }
}
