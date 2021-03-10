﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Client.GameObjects.Components;
using Robust.Client.ResourceManagement;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using static Robust.UnitTesting.RobustIntegrationTest;
using SpriteComponent = Robust.Client.GameObjects.SpriteComponent;

namespace Content.MapRenderer.Painters
{
    public class EntityPainter
    {
        private readonly ClientIntegrationInstance _client;

        private readonly IPrototypeManager _cPrototypeManager;
        private readonly IResourceCache _cResourceCache;
        private readonly IEntityManager _cEntityManager;
        private readonly IComponentManager _sComponentManager;
        private readonly IEntityManager _sEntityManager;

        private readonly ConcurrentDictionary<GridId, List<EntityData>> _entities;

        public EntityPainter(ClientIntegrationInstance client, ServerIntegrationInstance server)
        {
            _client = client;
            _cPrototypeManager = client.ResolveDependency<IPrototypeManager>();
            _cResourceCache = client.ResolveDependency<IResourceCache>();
            _cEntityManager = client.ResolveDependency<IEntityManager>();
            _sComponentManager = server.ResolveDependency<IComponentManager>();
            _sEntityManager = server.ResolveDependency<IEntityManager>();
            _entities = GetEntities();
        }

        public void Run(Image gridCanvas, IMapGrid grid)
        {
            if (!_entities.TryGetValue(grid.Index, out var walls))
            {
                Console.WriteLine($"No walls found on grid {grid.Index}");
                return;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var bounds = grid.WorldBounds;
            var xOffset = (int) Math.Abs(bounds.Left);
            var yOffset = (int) Math.Abs(bounds.Bottom);


            // TODO cache this shit what are we insane
            walls.Sort(Comparer<EntityData>.Create((x, y) => x.Sprite.DrawDepth.CompareTo(y.Sprite.DrawDepth)));

            foreach (var entity in walls.AsParallel())
            {
                if (entity.Sprite.Owner.HasComponent<SubFloorHideComponent>())
                {
                    continue;
                }

                if (!entity.Sprite.Visible || entity.Sprite.ContainerOccluded)
                {
                    continue;
                }

                var rotation = entity.Sprite.Owner.Transform.WorldRotation;
                var position = entity.Sprite.Owner.Transform.WorldPosition;

                foreach (var layer in entity.Sprite.AllLayers)
                {
                    if (!layer.Visible)
                    {
                        continue;
                    }

                    if (!layer.RsiState.IsValid)
                    {
                        continue;
                    }

                    var rsi = layer.ActualRsi;
                    Stream stream;

                    if (rsi == null || rsi.Path == null || !rsi.TryGetState(layer.RsiState, out var state))
                    {
                        stream = _cResourceCache.ContentFileRead("/Textures/error.rsi/error.png");
                    }
                    else if (!_cResourceCache.TryContentFileRead($"{rsi.Path}/full.png", out stream))
                    {
                        if (rsi.Path.ToString().EndsWith("low_wall.rsi"))
                        {
                            stream = _cResourceCache.ContentFileRead($"{rsi.Path}/metal.png");
                        }
                        else if (rsi.Path.ToString().EndsWith("catwalk.rsi"))
                        {
                            stream = _cResourceCache.ContentFileRead($"{rsi.Path}/catwalk_preview.png");
                        }
                        else
                        {
                            var stateId = state.StateId;
                            stream = _cResourceCache.ContentFileRead($"{rsi.Path}/{stateId}.png");
                        }
                    }

                    var image = Image.Load<Rgba32>(stream);

                    image.Mutate(o => o.Resize(32, 32).Flip(FlipMode.Vertical));

                    gridCanvas.Mutate(o => o.DrawImage(image, new Point((entity.X + xOffset) * 32, (entity.Y + yOffset) * 32), 1));
                }
            }

            Console.WriteLine($"{nameof(EntityPainter)} painted {walls.Count} walls on grid {grid.Index} in {(int) stopwatch.Elapsed.TotalMilliseconds} ms");
        }

        private ConcurrentDictionary<GridId, List<EntityData>> GetEntities()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var components = new ConcurrentDictionary<GridId, List<EntityData>>();

            foreach (var entity in _sEntityManager.GetEntities())
            {
                if (!entity.HasComponent<ISpriteRenderableComponent>())
                {
                    continue;
                }

                if (entity.Prototype == null)
                {
                    continue;
                }

                var clientEntity = _cEntityManager.GetEntity(entity.Uid);

                if (!clientEntity.TryGetComponent(out SpriteComponent sprite))
                {
                    throw new InvalidOperationException(
                        $"No sprite component found on an entity for which a server sprite component exists. Prototype id: {entity.Prototype?.ID}");
                }

                var position = entity.Transform.WorldPosition;
                var x = (int) Math.Floor(position.X);
                var y = (int) Math.Floor(position.Y);
                var data = new EntityData(sprite, x, y);

                components.GetOrAdd(entity.Transform.GridID, _ => new List<EntityData>()).Add(data);
            }

            Console.WriteLine($"Found {components.Values.Sum(l => l.Count)} walls on {components.Count} grids in {(int) stopwatch.Elapsed.TotalMilliseconds} ms");

            return components;
        }
    }
}
