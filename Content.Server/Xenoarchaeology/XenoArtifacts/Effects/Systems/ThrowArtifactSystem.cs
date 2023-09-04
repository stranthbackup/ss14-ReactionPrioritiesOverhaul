﻿using System.Numerics;
using Content.Server.Maps;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;
using Content.Shared.Maps;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

[InjectDependencies]
public sealed partial class ThrowArtifactSystem : EntitySystem
{
    [Dependency] private IMapManager _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private TileSystem _tile = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<ThrowArtifactComponent, ArtifactActivatedEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, ThrowArtifactComponent component, ArtifactActivatedEvent args)
    {
        var xform = Transform(uid);
        if (_map.TryGetGrid(xform.GridUid, out var grid))
        {
            var tiles = grid.GetTilesIntersecting(
                Box2.CenteredAround(xform.WorldPosition, new Vector2(component.Range * 2, component.Range)));

            foreach (var tile in tiles)
            {
                if (!_random.Prob(component.TilePryChance))
                    continue;

                _tile.PryTile(tile);
            }
        }

        var lookup = _lookup.GetEntitiesInRange(uid, component.Range, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var ent in lookup)
        {
            var tempXform = Transform(ent);

            var foo = tempXform.MapPosition.Position - xform.MapPosition.Position;
            _throwing.TryThrow(ent, foo*2, component.ThrowStrength, uid, 0);
        }
    }
}
