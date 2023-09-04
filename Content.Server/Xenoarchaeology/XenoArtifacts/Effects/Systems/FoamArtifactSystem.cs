﻿using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

[InjectDependencies]
public sealed partial class FoamArtifactSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SmokeSystem _smoke = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<FoamArtifactComponent, ArtifactNodeEnteredEvent>(OnNodeEntered);
        SubscribeLocalEvent<FoamArtifactComponent, ArtifactActivatedEvent>(OnActivated);
    }

    private void OnNodeEntered(EntityUid uid, FoamArtifactComponent component, ArtifactNodeEnteredEvent args)
    {
        if (!component.Reagents.Any())
            return;

        component.SelectedReagent = component.Reagents[args.RandomSeed % component.Reagents.Count];
    }

    private void OnActivated(EntityUid uid, FoamArtifactComponent component, ArtifactActivatedEvent args)
    {
        if (component.SelectedReagent == null)
            return;

        var sol = new Solution();
        var xform = Transform(uid);
        var range = (int) MathF.Round(MathHelper.Lerp(component.MinFoamAmount, component.MaxFoamAmount, _random.NextFloat(0, 1f)));
        sol.AddReagent(component.SelectedReagent, component.ReagentAmount);
        var foamEnt = Spawn("Foam", xform.Coordinates);
        var smoke = EnsureComp<SmokeComponent>(foamEnt);
        smoke.SpreadAmount = range * 4;
        _smoke.Start(foamEnt, smoke, sol, component.Duration);
    }
}
