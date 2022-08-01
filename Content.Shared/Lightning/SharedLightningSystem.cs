﻿using Content.Shared.Actions;
using Content.Shared.Interaction;
using Content.Shared.Lightning.Components;

namespace Content.Shared.Lightning;

public sealed class SharedLightningSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SharedLightningComponent, InteractHandEvent>(OnHandInteract);
        SubscribeLocalEvent<SharedLightningComponent, WorldTargetActionEvent>(OnWorldTarget);
    }

    private void OnWorldTarget(EntityUid uid, SharedLightningComponent component, WorldTargetActionEvent args)
    {
        SpawnLightning(component);
    }

    private void OnHandInteract(EntityUid uid, SharedLightningComponent component, InteractHandEvent args)
    {
        SpawnLightning(component);
    }

    //TODO: Add way to form the lightning (sprites/spawning)

    public void SpawnLightning(SharedLightningComponent component)
    {

    }

    //TODO: Make fixture with shape (edge/AABB/poly) so the body and impact can shock

    //TODO: Scale the body of the sprite and the fixture.

    //TODO: Add Electrocution Component

    //TODO: Use TimedDespawn to handle the deletion

    //TODO: Add way to arc/chain lightning
}
