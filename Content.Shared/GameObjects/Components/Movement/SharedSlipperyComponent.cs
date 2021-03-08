﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.EntitySystems.EffectBlocker;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Movement
{
    public abstract class SharedSlipperyComponent : Component, IStartCollide
    {
        public sealed override string Name => "Slippery";

        /// <summary>
        ///     The list of entities that have been slipped by this component,
        ///     and which have not stopped colliding with its owner yet.
        /// </summary>
        protected readonly List<EntityUid> _slipped = new();

        /// <summary>
        ///     How many seconds the mob will be paralyzed for.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("paralyzeTime")]
        public virtual float ParalyzeTime { get; set; } = 3f;

        /// <summary>
        ///     Percentage of shape intersection for a slip to occur.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("intersectPercentage")]
        public virtual float IntersectPercentage { get; set; } = 0.3f;

        /// <summary>
        ///     Entities will only be slipped if their speed exceeds this limit.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("requiredSlipSpeed")]
        public virtual float RequiredSlipSpeed { get; set; } = 0.1f;

        /// <summary>
        ///     The entity's speed will be multiplied by this to slip it forwards.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("launchForwardsMultiplier")]
        public virtual float LaunchForwardsMultiplier { get; set; } = 1f;

        /// <summary>
        ///     Whether or not this component will try to slip entities.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("slippery")]
        public virtual bool Slippery { get; set; } = true;

        private bool TrySlip(IPhysBody ourBody, IPhysBody otherBody)
        {
            if (!Slippery
                || Owner.IsInContainer()
                ||  _slipped.Contains(otherBody.Entity.Uid)
                ||  !otherBody.Entity.TryGetComponent(out SharedStunnableComponent? stun))
            {
                return false;
            }

            if (otherBody.LinearVelocity.Length < RequiredSlipSpeed || stun.KnockedDown)
            {
                return false;
            }

            var percentage = otherBody.GetWorldAABB().IntersectPercentage(ourBody.GetWorldAABB());

            if (percentage < IntersectPercentage)
            {
                return false;
            }

            if (!EffectBlockerSystem.CanSlip(otherBody.Entity))
            {
                return false;
            }

            otherBody.LinearVelocity *= LaunchForwardsMultiplier;

            stun.Paralyze(5);
            _slipped.Add(otherBody.Entity.Uid);

            OnSlip();

            return true;
        }

        protected virtual void OnSlip() { }

        void IStartCollide.CollideWith(IPhysBody ourBody, IPhysBody otherBody, in Manifold manifold)
        {
            TrySlip(ourBody, otherBody);
        }

        public void Update()
        {
            foreach (var uid in _slipped.ToArray())
            {
                if (!uid.IsValid() || !Owner.EntityManager.EntityExists(uid))
                {
                    _slipped.Remove(uid);
                    continue;
                }

                var entity = Owner.EntityManager.GetEntity(uid);
                var physics = Owner.GetComponent<IPhysBody>();
                var otherPhysics = entity.GetComponent<IPhysBody>();

                if (!physics.GetWorldAABB().Intersects(otherPhysics.GetWorldAABB()))
                {
                    _slipped.Remove(uid);
                }
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            var physics = Owner.EnsureComponent<PhysicsComponent>();

            physics.Hard = false;

            var fixtures = physics.Fixtures.FirstOrDefault();

            if (fixtures != null)
            {
                fixtures.CollisionLayer |= (int) CollisionGroup.SmallImpassable;
                fixtures.CollisionMask = (int) CollisionGroup.None;
            }
        }
    }

    [Serializable, NetSerializable]
    public class SlipperyComponentState : ComponentState
    {
        public float ParalyzeTime { get; }
        public float IntersectPercentage { get; }
        public float RequiredSlipSpeed { get; }
        public float LaunchForwardsMultiplier { get; }
        public bool Slippery { get; }

        public SlipperyComponentState(float paralyzeTime, float intersectPercentage, float requiredSlipSpeed, float launchForwardsMultiplier, bool slippery) : base(ContentNetIDs.SLIP)
        {
            ParalyzeTime = paralyzeTime;
            IntersectPercentage = intersectPercentage;
            RequiredSlipSpeed = requiredSlipSpeed;
            LaunchForwardsMultiplier = launchForwardsMultiplier;
            Slippery = slippery;
        }
    }
}
