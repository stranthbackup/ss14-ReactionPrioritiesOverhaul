﻿#nullable enable
using System;
using Content.Shared.GameObjects.Components.Movement;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Shared.Physics
{
    public class MoverController : VirtualController
    {
        public override IPhysicsComponent? ControlledComponent { protected get; set; }

        private float timeToVmax = 0.2f;
        private float minDesiredTimeStep = 1 / 30f;

        private Vector2 StepSimulation(Vector2 velocityDirection, Vector2 linearVelocity, float dragCoeff, float mass, float frameTime)
        {
            Vector2 thrustForce = velocityDirection * dragCoeff * mass;

            Vector2 netForce = thrustForce - linearVelocity * dragCoeff * mass;
            Vector2 a = netForce / mass;
            Vector2 k1 = a * frameTime;

            netForce = thrustForce - (linearVelocity + k1/2) * dragCoeff * mass;
            a = netForce / mass;
            Vector2 k2 = a * frameTime;

            netForce = thrustForce - (linearVelocity + k2/2) * dragCoeff * mass;
            a = netForce / mass;
            Vector2 k3 = a * frameTime;

            netForce = thrustForce - (linearVelocity + k3) * dragCoeff * mass;
            a = netForce / mass;
            Vector2 k4 = a * frameTime;

            Vector2 deltaV = (k1 + k2*2 + k3*2 + k4) / 6;
            return deltaV;
        }

        public void Move(Vector2 velocityDirection, float frameTime)
        {
            if (ControlledComponent == null)
            {
                return;
            }
            //apply a counteracting force to the standard friction between a human and a floor
            //TODO: friction should involve mass, but in the current physics system it doesn't so we can't have it here either
            //Vector2 antiFriction = velocityDirection.Normalized * (0.35f * 9.8f * frameTime);

            float mass = ControlledComponent.Mass;
            float dragCoeff = 5 / timeToVmax;
            if (ControlledComponent.Owner.IsWeightless())
            {
                dragCoeff /= 50; //ice level time
            }
            Vector2 linearVelocity = ControlledComponent.LinearVelocity;

            var multiplier = frameTime / minDesiredTimeStep;
            var divisions = Math.Max(1, MathF.Round(multiplier, MidpointRounding.AwayFromZero));
            if (divisions > 10)
            {
                return; //hah hah holy hell brother you have worse problems than players being unable to move
            }

            var timeStep = frameTime / divisions;
            Vector2 deltaV = Vector2.Zero;
            for (var i = 0; i < divisions; i++)
            {
                deltaV += StepSimulation(velocityDirection, linearVelocity + deltaV, dragCoeff, mass, timeStep);
            }
            Impulse = deltaV * mass / frameTime;
        }

        public void StopMoving(float frameTime)
        {
            if (ControlledComponent == null || ControlledComponent.Owner.IsWeightless())
            {
                return;
            }

            //apply a counteracting force to the standard friction between a human and a floor
            //TODO: friction should involve mass, but in the current physics system it doesn't so we can't have it here either
            //Vector2 antiFriction = velocityDirection.Normalized * (0.35f * 9.8f * frameTime);

            var linearVelocity = ControlledComponent.LinearVelocity;
            if (MathHelper.CloseTo(linearVelocity.LengthSquared, 0))
            {
                return;
            }

            float mass = ControlledComponent.Mass;
            float dragCoeff = 5 / timeToVmax;

            var multiplier = frameTime / minDesiredTimeStep;
            var divisions = Math.Max(1, MathF.Round(multiplier, MidpointRounding.AwayFromZero));
            if (divisions > 10)
            {
                return; //hah hah holy hell brother you have worse problems than players being unable to move
            }

            var timeStep = frameTime / divisions;
            Vector2 deltaV = Vector2.Zero;
            for (var i = 0; i < divisions; i++)
            {
                deltaV += StepSimulation(Vector2.Zero, linearVelocity + deltaV, dragCoeff, mass, timeStep);
            }
            Impulse = deltaV * mass / frameTime;
        }
    }
}
