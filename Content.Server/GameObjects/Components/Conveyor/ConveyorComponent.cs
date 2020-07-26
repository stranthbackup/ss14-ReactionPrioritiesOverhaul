﻿#nullable enable
using Content.Server.GameObjects.Components.Interactable;
using Content.Server.GameObjects.Components.Power.ApcNetComponents;
using Content.Server.GameObjects.EntitySystems;
using Content.Server.GameObjects.EntitySystems.Click;
using Content.Shared.GameObjects.Components.Conveyor;
using Content.Shared.GameObjects.Components.Interactable;
using Content.Shared.Interfaces.GameObjects.Components;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Map;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Conveyor
{
    [RegisterComponent]
    public class ConveyorComponent : Component, IExamine, IInteractUsing
    {
#pragma warning disable 649
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
#pragma warning restore 649

        public override string Name => "Conveyor";

        /// <summary>
        ///     The angle to move entities by in relation to the owner's rotation.
        /// </summary>
        [ViewVariables]
        private Angle _angle;

        /// <summary>
        ///     The amount of units to move the entity by per second.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        private float _speed;

        [ViewVariables]
        private uint? _id;

        private ConveyorState _state;

        /// <summary>
        ///     The current state of this conveyor
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        private ConveyorState State
        {
            get => _state;
            set
            {
                _state = value;

                if (!Owner.TryGetComponent(out AppearanceComponent appearance))
                {
                    return;
                }

                appearance.SetData(ConveyorVisuals.State, value);
            }
        }

        private ConveyorGroup? Group => _id.HasValue ? EntitySystem.Get<ConveyorSystem>().EnsureGroup(_id.Value) : null;

        /// <summary>
        ///     Calculates the angle in which entities on top of this conveyor
        ///     belt are pushed in
        /// </summary>
        /// <returns>
        ///     The angle when taking into account if the conveyor is reversed
        /// </returns>
        private Angle GetAngle()
        {
            var adjustment = _state == ConveyorState.Reversed ? MathHelper.Pi : 0;
            var radians = MathHelper.DegreesToRadians(_angle);

            return new Angle(Owner.Transform.LocalRotation.Theta + radians + adjustment);
        }

        private bool CanRun()
        {
            if (State == ConveyorState.Off)
            {
                return false;
            }

            if (Owner.TryGetComponent(out PowerReceiverComponent receiver) &&
                !receiver.Powered)
            {
                return false;
            }

            if (Owner.HasComponent<ItemComponent>())
            {
                return false;
            }

            return true;
        }

        private bool CanMove(IEntity entity)
        {
            if (entity == Owner)
            {
                return false;
            }

            if (!entity.TryGetComponent(out ICollidableComponent collidable) ||
                collidable.Anchored)
            {
                return false;
            }

            if (entity.HasComponent<ConveyorComponent>())
            {
                return false;
            }

            if (entity.HasComponent<IMapGridComponent>())
            {
                return false;
            }

            if (ContainerHelpers.IsInContainer(entity))
            {
                return false;
            }

            return true;
        }

        public void Update()
        {
            if (!CanRun())
            {
                return;
            }

            var intersecting = _entityManager.GetEntitiesIntersecting(Owner, true);
            var direction = GetAngle().ToVec();

            foreach (var entity in intersecting)
            {
                if (!CanMove(entity))
                {
                    continue;
                }

                if (entity.TryGetComponent(out ICollidableComponent collidable))
                {
                    var controller = collidable.EnsureController<ConveyedController>();
                    controller.Move(direction, _speed);
                }
            }
        }

        private bool ToolUsed(IEntity user, ToolComponent tool)
        {
            if (!Owner.HasComponent<ItemComponent>() &&
                tool.UseTool(user, Owner, ToolQuality.Prying))
            {
                State = ConveyorState.Loose;

                Owner.AddComponent<ItemComponent>();
                Group?.RemoveConveyor(this);
                Owner.Transform.WorldPosition += (_random.NextFloat() * 0.4f - 0.2f, _random.NextFloat() * 0.4f - 0.2f);

                return true;
            }

            return false;
        }

        public void Sync(ConveyorGroup group)
        {
            _id = group.Id;

            if (State == ConveyorState.Loose)
            {
                return;
            }

            State = group.State == ConveyorState.Loose
                ? ConveyorState.Off
                : group.State;
        }

        public void Disconnect()
        {
            _id = 0;
            State = ConveyorState.Off;
        }

        public void ChangeId(uint id)
        {
            EntitySystem.Get<ConveyorSystem>().ChangeId(this, _id, id);
        }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _id, "id", null);
            serializer.DataField(ref _angle, "angle", 0);
            serializer.DataField(ref _speed, "speed", 2);
        }

        public override void Initialize()
        {
            base.Initialize();

            if (_id == null)
            {
                return;
            }

            EntitySystem.Get<ConveyorSystem>().EnsureGroup(_id.Value).AddConveyor(this);
        }

        public override void OnRemove()
        {
            base.OnRemove();

            Group?.RemoveConveyor(this);
        }

        void IExamine.Examine(FormattedMessage message, bool inDetailsRange)
        {
            var tooltip = _id.HasValue
                ? Loc.GetString("Its switch has an id of {0}.", _id.Value)
                : Loc.GetString("It doesn't have an associated switch.");

            message.AddMarkup(tooltip);
        }

        bool IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (eventArgs.Using.TryGetComponent(out ConveyorSwitchComponent conveyorSwitch))
            {
                conveyorSwitch.Connect(eventArgs.User, this);
                return true;
            }

            if (eventArgs.Using.TryGetComponent(out ToolComponent tool))
            {
                return ToolUsed(eventArgs.User, tool);
            }

            return false;
        }
    }
}
