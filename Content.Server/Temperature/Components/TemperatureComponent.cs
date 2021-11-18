using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Temperature.Components
{
    /// <summary>
    /// Handles changing temperature,
    /// informing others of the current temperature,
    /// and taking fire damage from high temperature.
    /// </summary>
    [RegisterComponent]
    public class TemperatureComponent : Component
    {
        /// <inheritdoc />
        public override string Name => "Temperature";

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentTemperature { get; set; } = Atmospherics.T20C;

        [DataField("heatDamageThreshold")]
        [ViewVariables]
        public float HeatDamageThreshold;

        [DataField("coldDamageThreshold")]
        [ViewVariables]
        public float ColdDamageThreshold;

        [DataField("specificHeat")]
        [ViewVariables]
        public float SpecificHeat;

        [ViewVariables] public float HeatCapacity
        {
            get
            {
                if (Owner.TryGetComponent<IPhysBody>(out var physics))
                {
                    return SpecificHeat * physics.Mass;
                }

                return Atmospherics.MinimumHeatCapacity;
            }
        }

        [DataField("coldDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier ColdDamage = default!;

        [DataField("heatDamage", required: true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier HeatDamage = default!;

        /// <summary>
        ///     Temperature won't do more than this amount of damage per second.
        ///
        ///     Okay it genuinely reaches this basically immediately for a plasma fire.
        /// </summary>
        [DataField("damageCap")]
        [ViewVariables(VVAccess.ReadWrite)]
        public FixedPoint2 DamageCap = FixedPoint2.New(8);
    }
}
