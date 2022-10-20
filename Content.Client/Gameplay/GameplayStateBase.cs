using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.Clickable;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Containers;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Gameplay
{
    // OH GOD.
    // Ok actually it's fine.
    // Instantiated dynamically through the StateManager, Dependencies will be resolved.
    [Virtual]
    public class GameplayStateBase : State, IEntityEventSubscriber
    {
        [Dependency] private readonly IInputManager _inputManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] protected readonly IUserInterfaceManager UserInterfaceManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private ClickableEntityComparer _comparer = default!;

        protected override void Startup()
        {
            _inputManager.KeyBindStateChanged += OnKeyBindStateChanged;
            _comparer = new ClickableEntityComparer(_entityManager);
        }

        protected override void Shutdown()
        {
            _inputManager.KeyBindStateChanged -= OnKeyBindStateChanged;
        }

        public EntityUid? GetEntityUnderPosition(MapCoordinates coordinates)
        {
            var entitiesUnderPosition = GetEntitiesUnderPosition(coordinates);
            return entitiesUnderPosition.Count > 0 ? entitiesUnderPosition[0] : null;
        }

        public IList<EntityUid> GetEntitiesUnderPosition(EntityCoordinates coordinates)
        {
            return GetEntitiesUnderPosition(coordinates.ToMap(_entityManager));
        }

        public IList<EntityUid> GetEntitiesUnderPosition(MapCoordinates coordinates)
        {
            // Find all the entities intersecting our click
            var entities = EntitySystem.Get<EntityLookupSystem>().GetEntitiesIntersecting(coordinates.MapId,
                Box2.CenteredAround(coordinates.Position, (1, 1)));

            var eye = IoCManager.Resolve<IEyeManager>().CurrentEye;

            var containerSystem = _entitySystemManager.GetEntitySystem<SharedContainerSystem>();

            // Check the entities against whether or not we can click them
            var foundEntities = new List<(EntityUid clicked, int drawDepth, uint renderOrder, float top)>();
            var clickQuery = _entityManager.GetEntityQuery<ClickableComponent>();
            var metaQuery = _entityManager.GetEntityQuery<MetaDataComponent>();
            var spriteQuery = _entityManager.GetEntityQuery<SpriteComponent>();
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();

            foreach (var entity in entities)
            {
                if (clickQuery.TryGetComponent(entity, out var component) &&
                    metaQuery.TryGetComponent(entity, out var meta) &&
                    !containerSystem.IsEntityInContainer(entity, meta) &&
                    spriteQuery.TryGetComponent(entity, out var sprite) &&
                    component.CheckClick(sprite, xformQuery, coordinates.Position, eye, out var drawDepthClicked, out var renderOrder, out var top))
                {
                    foundEntities.Add((entity, drawDepthClicked, renderOrder, top));
                }
            }

            if (foundEntities.Count == 0)
                return Array.Empty<EntityUid>();

            foundEntities.Sort(_comparer);
            foundEntities.Reverse();
            // 0 is the top element.
            return foundEntities.Select(a => a.clicked).ToList();
        }

        private sealed class ClickableEntityComparer : IComparer<(EntityUid clicked, int depth, uint renderOrder, float top)>
        {
            private readonly IEntityManager _entities;

            public ClickableEntityComparer(IEntityManager entities)
            {
                _entities = entities;
            }

            public int Compare((EntityUid clicked, int depth, uint renderOrder, float top) x,
                (EntityUid clicked, int depth, uint renderOrder, float top) y)
            {
                var cmp = x.depth.CompareTo(y.depth);
                if (cmp != 0)
                {
                    return cmp;
                }

                cmp = x.renderOrder.CompareTo(y.renderOrder);

                if (cmp != 0)
                {
                    return cmp;
                }

                // compare the top of the sprite's BB for y-sorting. Because screen coordinates are flipped, the "top" of the BB is actually the "bottom".
                cmp = x.top.CompareTo(y.top);

                if (cmp != 0)
                {
                    return cmp;
                }

                return x.clicked.CompareTo(y.clicked);
            }
        }

        /// <summary>
        ///     Converts a state change event from outside the simulation to inside the simulation.
        /// </summary>
        /// <param name="args">Event data values for a bound key state change.</param>
        protected virtual void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
        {
            // If there is no InputSystem, then there is nothing to forward to, and nothing to do here.
            if(!_entitySystemManager.TryGetEntitySystem(out InputSystem? inputSys))
                return;

            var kArgs = args.KeyEventArgs;
            var func = kArgs.Function;
            var funcId = _inputManager.NetworkBindMap.KeyFunctionID(func);

            EntityCoordinates coordinates = default;
            EntityUid? entityToClick = null;
            if (args.Viewport is IViewportControl vp)
            {
                var mousePosWorld = vp.ScreenToMap(kArgs.PointerLocation.Position);
                entityToClick = GetEntityUnderPosition(mousePosWorld);

                coordinates = _mapManager.TryFindGridAt(mousePosWorld, out var grid) ? grid.MapToGrid(mousePosWorld) :
                    EntityCoordinates.FromMap(_mapManager, mousePosWorld);
            }

            var message = new FullInputCmdMessage(_timing.CurTick, _timing.TickFraction, funcId, kArgs.State,
                coordinates , kArgs.PointerLocation,
                entityToClick ?? default); // TODO make entityUid nullable

            // client side command handlers will always be sent the local player session.
            var session = _playerManager.LocalPlayer?.Session;
            if (inputSys.HandleInputCommand(session, func, message))
            {
                kArgs.Handle();
            }
        }
    }
}
