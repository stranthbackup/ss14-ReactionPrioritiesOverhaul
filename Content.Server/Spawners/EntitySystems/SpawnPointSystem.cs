﻿using Content.Server.GameTicking;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

[InjectDependencies]
public sealed partial class SpawnPointSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawningEvent>(OnSpawnPlayer);
    }

    private void OnSpawnPlayer(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // TODO: Cache all this if it ends up important.
        var points = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        var possiblePositions = new List<EntityCoordinates>();

        while ( points.MoveNext(out var uid, out var spawnPoint, out var xform))
        {
            if (args.Station != null && _stationSystem.GetOwningStation(uid, xform) != args.Station)
                continue;

            if (_gameTicker.RunLevel == GameRunLevel.InRound && spawnPoint.SpawnType == SpawnPointType.LateJoin)
            {
                possiblePositions.Add(xform.Coordinates);
            }

            if (_gameTicker.RunLevel != GameRunLevel.InRound &&
                spawnPoint.SpawnType == SpawnPointType.Job &&
                (args.Job == null || spawnPoint.Job?.ID == args.Job.PrototypeId))
            {
                possiblePositions.Add(xform.Coordinates);
            }
        }

        if (possiblePositions.Count == 0)
        {
            // Ok we've still not returned, but we need to put them /somewhere/.
            // TODO: Refactor gameticker spawning code so we don't have to do this!
            var points2 = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();

            if (points2.MoveNext(out var uid, out var spawnPoint, out var xform))
            {
                possiblePositions.Add(xform.Coordinates);
            }
            else
            {
                Log.Error("No spawn points were available!");
                return;
            }
        }

        var spawnLoc = _random.Pick(possiblePositions);

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            spawnLoc,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);
    }
}
