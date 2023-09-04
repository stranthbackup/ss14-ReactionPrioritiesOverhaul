﻿using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

[InjectDependencies]
public sealed partial class RandomTeleportArtifactSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RandomTeleportArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, RandomTeleportArtifactComponent component, ArtifactActivatedEvent args)
    {
        var xform = Transform(uid);
        _popup.PopupCoordinates(Loc.GetString("blink-artifact-popup"), xform.Coordinates, PopupType.Medium);

        _xform.SetCoordinates(uid, xform, xform.Coordinates.Offset(_random.NextVector2(component.MinRange, component.MaxRange)));
    }
}
