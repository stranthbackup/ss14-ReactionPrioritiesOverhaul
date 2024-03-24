﻿using Content.Shared.Medical.Consciousness.Components;
using Content.Shared.Medical.Consciousness.Systems;
using Content.Shared.Medical.Pain.Components;
using Content.Shared.Medical.Pain.Systems;
using Content.Shared.Medical.Trauma.Components;
using Content.Shared.Medical.Wounding.Events;
using Content.Shared.Medical.Wounding.Systems;
using Content.Shared.Mobs.Components;

namespace Content.Shared.Medical.Trauma.Systems;

public sealed partial class TraumaSystem : EntitySystem
{

    [Dependency] private readonly ConsciousnessSystem _consciousnessSystem = default!;
    [Dependency] private readonly PainSystem _painSystem = default!;
    [Dependency] private readonly WoundSystem _woundSystem = default!;

    public override void Initialize()
    {
        SubscribeTraumaWoundEvents<HealthTraumaComponent>(ApplyHealthTrauma, RemoveHealthTrauma);
        SubscribeTraumaWoundEvents<IntegrityTraumaComponent>(ApplyIntegrityTrauma, RemoveIntegrityTrauma);

        SubscribeTraumaBodyEvents<ConsciousnessTraumaComponent>(ApplyConsciousnessTrauma, RemoveConsciousnessTrauma);
        SubscribeTraumaBodyEvents<PainTraumaComponent>(ApplyPainTrauma, RemovePainTrauma);
    }

    private void ApplyPainTrauma(EntityUid uid, PainTraumaComponent pain, ref WoundAppliedToBody args)
    {
        _painSystem.ChangePain(new Entity<NervousSystemComponent?>(uid, null), pain.PainDelta);
        _painSystem.ChangePainCap(new Entity<NervousSystemComponent?>(uid, null), pain.PainCapDelta);
        _painSystem.ChangeMitigation(new Entity<NervousSystemComponent?>(uid, null), pain.PainMitigationDelta);
        _painSystem.ChangeMitigation(new Entity<NervousSystemComponent?>(uid, null), pain.PainMultiplierDelta);
    }

    private void RemovePainTrauma(EntityUid uid, PainTraumaComponent pain, ref WoundRemovedFromBody args)
    {
        _painSystem.ChangePain(new Entity<NervousSystemComponent?>(uid, null), -pain.PainDelta);
        _painSystem.ChangePainCap(new Entity<NervousSystemComponent?>(uid, null), -pain.PainCapDelta);
        _painSystem.ChangeMitigation(new Entity<NervousSystemComponent?>(uid, null), -pain.PainMitigationDelta);
        _painSystem.ChangeMitigation(new Entity<NervousSystemComponent?>(uid, null), -pain.PainMultiplierDelta);
    }


    private void ApplyConsciousnessTrauma(EntityUid uid, ConsciousnessTraumaComponent trauma, ref WoundAppliedToBody args)
    {
        _consciousnessSystem.AddConsciousnessCap(
            new Entity<ConsciousnessComponent?, MobStateComponent?>(args.Body.Owner, null, null),
            trauma.CapDelta);
    }
    private void RemoveConsciousnessTrauma(EntityUid uid, ConsciousnessTraumaComponent trauma, ref WoundRemovedFromBody args)
    {
        _consciousnessSystem.AddConsciousnessCap(
            new Entity<ConsciousnessComponent?, MobStateComponent?>(args.Body.Owner, null, null),
            -trauma.CapDelta);
    }

    private void ApplyIntegrityTrauma(EntityUid uid, IntegrityTraumaComponent trauma, ref WoundCreatedEvent args)
    {
        _woundSystem.ChangeIntegrity(args.ParentWoundable, trauma.IntegrityDelta);
        _woundSystem.ChangeIntegrityCap(args.ParentWoundable, trauma.IntegrityCapDelta);
    }

    private void RemoveIntegrityTrauma(EntityUid uid, IntegrityTraumaComponent trauma, ref WoundDestroyedEvent args)
    {
        _woundSystem.ChangeIntegrity(args.ParentWoundable, -trauma.IntegrityDelta);
        _woundSystem.ChangeIntegrityCap(args.ParentWoundable, -trauma.IntegrityCapDelta);
    }

    private void ApplyHealthTrauma(EntityUid uid, HealthTraumaComponent trauma, ref WoundCreatedEvent args)
    {
        _woundSystem.ChangeHealthCap(args.ParentWoundable, trauma.HealthCapDelta);
    }

    private void RemoveHealthTrauma(EntityUid uid, HealthTraumaComponent trauma, ref WoundDestroyedEvent args)
    {
        _woundSystem.ChangeHealthCap(args.ParentWoundable, -trauma.HealthCapDelta);
    }

    #region Helpers

    protected void SubscribeTraumaWoundEvents<T1>(
        ComponentEventRefHandler<T1,
            WoundCreatedEvent> woundCreated,ComponentEventRefHandler<T1,
            WoundDestroyedEvent> woundDestroyed) where T1: Component, new()
    {
        SubscribeLocalEvent(woundCreated);
        SubscribeLocalEvent(woundDestroyed);
    }

    protected void SubscribeTraumaBodyEvents<T1>(
        ComponentEventRefHandler<T1,
            WoundAppliedToBody> attachedToBody,ComponentEventRefHandler<T1,
            WoundRemovedFromBody> detachedFromBody) where T1: Component, new()
    {
        SubscribeLocalEvent(attachedToBody);
        SubscribeLocalEvent(detachedFromBody);
    }
    #endregion


}
