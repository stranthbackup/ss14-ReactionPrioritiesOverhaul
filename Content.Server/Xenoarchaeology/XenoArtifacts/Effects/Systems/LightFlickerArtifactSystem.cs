﻿using Content.Server.Ghost;
using Content.Server.Light.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

/// <summary>
/// This handles...
/// </summary>
[InjectDependencies]
public sealed partial class LightFlickerArtifactSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private GhostSystem _ghost = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<LightFlickerArtifactComponent, ArtifactActivatedEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, LightFlickerArtifactComponent component, ArtifactActivatedEvent args)
    {
        var lights = GetEntityQuery<PoweredLightComponent>();
        foreach (var light in _lookup.GetEntitiesInRange(uid, component.Radius, LookupFlags.StaticSundries ))
        {
            if (!lights.HasComponent(light))
                continue;

            if (!_random.Prob(component.FlickerChance))
                continue;

            _ghost.DoGhostBooEvent(light);
        }
    }
}
