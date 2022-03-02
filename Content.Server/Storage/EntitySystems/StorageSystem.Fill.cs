using Content.Server.Storage.Components;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Storage.EntitySystems;

public sealed partial class StorageSystem
{
    private void OnStorageFillMapInit(EntityUid uid, StorageFillComponent component, MapInitEvent args)
    {
        if (component.Contents.Count == 0) return;

        if (!TryComp<IStorageComponent>(uid, out var storage))
        {
            Logger.Error($"StorageFillComponent couldn't find any StorageComponent ({uid})");
            return;
        }

        var coordinates = Transform(uid).Coordinates;

        var orGroupedSpawns = new Dictionary<string, OrGroup>();

        // collect groups together, create singular items that pass probability
        foreach (var entry in component.Contents)
        {
            // Handle "Or" groups
            if (!string.IsNullOrEmpty(entry.GroupId))
            {
                if (!orGroupedSpawns.TryGetValue(entry.GroupId, out OrGroup? orGroup))
                {
                    OrGroup currentGroup = new(new List<EntitySpawnEntry>(), 0f);
                    currentGroup.Entries.Add(entry);
                    currentGroup.CumulativeProbability += entry.SpawnProbability;
                    orGroupedSpawns.Add(entry.GroupId, currentGroup);
                    continue;
                }
                orGroup.Entries.Add(entry);
                orGroup.CumulativeProbability += entry.SpawnProbability;
                continue;
            }

            // else
            // Check random spawn
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (entry.SpawnProbability != 1f && !_random.Prob(entry.SpawnProbability)) continue;

            for (var i = 0; i < entry.Amount; i++)
            {
                var ent = EntityManager.SpawnEntity(entry.PrototypeId, coordinates);

                if (storage.Insert(ent)) continue;

                Logger.ErrorS("storage", $"Tried to StorageFill {entry.PrototypeId} inside {uid} but can't.");
                EntityManager.DeleteEntity(ent);
            }
        }

        // handle orgroup spawns
        foreach (var group in orGroupedSpawns)
        {
            double diceRoll = _random.NextDouble() * group.Value.CumulativeProbability;
            List<EntitySpawnEntry> shuffled = group.Value.Entries.OrderBy(a => _random.Next()).ToList();

            double cumulative = 0.0;
            for (int i = 0; i < shuffled.Count; i++)
            {
                cumulative += shuffled[i].SpawnProbability;
                if (diceRoll <= cumulative)
                {
                    for (var index = 0; index < shuffled[i].Amount; index++)
                    {
                        var ent = EntityManager.SpawnEntity(shuffled[i].PrototypeId, coordinates);

                        if (storage.Insert(ent)) continue;

                        Logger.ErrorS("storage", $"Tried to StorageFill {shuffled[i].PrototypeId} inside {uid} but can't.");
                        EntityManager.DeleteEntity(ent);
                    }
                    break;
                }
            }
        }
    }
    private sealed class OrGroup
    {
        public List<EntitySpawnEntry> Entries { get; set; } = new();
        public float CumulativeProbability { get; set; } = 0f;
        public OrGroup(List<EntitySpawnEntry> entries, float cumulativeProbability)
        {
            Entries = entries;
            CumulativeProbability = cumulativeProbability;
        }
    }
}
