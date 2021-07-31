using System;
using System.Collections.Generic;
using Content.Server.Alert;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.MobState;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Players;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;
using Robust.Shared.Prototypes;

namespace Content.Server.Nutrition.Components
{
    [RegisterComponent]
    public sealed class HungerComponent : SharedHungerComponent
    {
        [Dependency] private readonly IRobustRandom _random = default!;

        [DataField("baseDecayRate")]
        private float _baseDecayRate = 0.1f;

        // TODO PROTOTYPE Replace this datafield variable with prototype references, once they are supported.
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [DataField("damageType", required: true)]
        private readonly string _damageTypeID = default!;
        private DamageTypePrototype _damageType => _prototypeManager.Index<DamageTypePrototype>(_damageTypeID);

        [DataField("damageRecoveredPerSecond")]
        private float _damageRecoveredPerSecond = 0.1f;

        private float _acumulatedDamageRecovery;
        private float _actualDecayRate;
        private float _currentHunger;
        private HungerThreshold _currentHungerThreshold;
        private HungerThreshold _lastHungerThreshold;
        private readonly Dictionary<HungerThreshold, float> _hungerThresholds = new()
        {
            {HungerThreshold.Overfed, 600.0f},
            {HungerThreshold.Okay, 450.0f},
            {HungerThreshold.Peckish, 300.0f},
            {HungerThreshold.Starving, 150.0f},
            {HungerThreshold.Dead, 0.0f},
        };

        // TODO QUESTION Just based on DrSmugleaf's other comments on similar situations: are all of _baseDecayRate,
        // _actualDecayRate, _currentHunger all redundant here? i.e., shouldn't it just be:
        //    [ViewVariables(VVAccess.ReadWrite)]
        //    [DataField("baseDecayRate")]
        //    private float BaseDecayRate { get; set; } = 0.1f;
        // and similar for the others?

        // Base stuff
        [ViewVariables(VVAccess.ReadWrite)]
        public float BaseDecayRate
        {
            get => _baseDecayRate;
            set => _baseDecayRate = value;
        }

        [ViewVariables(VVAccess.ReadWrite)]
        public float ActualDecayRate
        {
            get => _actualDecayRate;
            set => _actualDecayRate = value;
        }

        // Hunger
        [ViewVariables(VVAccess.ReadOnly)]
        public override HungerThreshold CurrentHungerThreshold => _currentHungerThreshold;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentHunger
        {
            get => _currentHunger;
            set => _currentHunger = value;
        }


        [ViewVariables(VVAccess.ReadOnly)]
        public Dictionary<HungerThreshold, float> HungerThresholds => _hungerThresholds;

        public static readonly Dictionary<HungerThreshold, AlertType> HungerThresholdAlertTypes = new()
        {
            { HungerThreshold.Overfed, AlertType.Overfed },
            { HungerThreshold.Peckish, AlertType.Peckish },
            { HungerThreshold.Starving, AlertType.Starving },
        };

        public void HungerThresholdEffect(bool force = false)
        {
            if (_currentHungerThreshold != _lastHungerThreshold || force)
            {
                // Revert slow speed if required
                if (_lastHungerThreshold == HungerThreshold.Starving && _currentHungerThreshold != HungerThreshold.Dead &&
                    Owner.TryGetComponent(out MovementSpeedModifierComponent? movementSlowdownComponent))
                {
                    movementSlowdownComponent.RefreshMovementSpeedModifiers();
                }

                // Update UI
                Owner.TryGetComponent(out ServerAlertsComponent? alertsComponent);

                if (HungerThresholdAlertTypes.TryGetValue(_currentHungerThreshold, out var alertId))
                {
                    alertsComponent?.ShowAlert(alertId);
                }
                else
                {
                    alertsComponent?.ClearAlertCategory(AlertCategory.Hunger);
                }

                switch (_currentHungerThreshold)
                {
                    case HungerThreshold.Overfed:
                        _lastHungerThreshold = _currentHungerThreshold;
                        _actualDecayRate = _baseDecayRate * 1.2f;
                        return;

                    case HungerThreshold.Okay:
                        _lastHungerThreshold = _currentHungerThreshold;
                        _actualDecayRate = _baseDecayRate;
                        return;

                    case HungerThreshold.Peckish:
                        // Same as okay except with UI icon saying eat soon.
                        _lastHungerThreshold = _currentHungerThreshold;
                        _actualDecayRate = _baseDecayRate * 0.8f;
                        return;

                    case HungerThreshold.Starving:
                        // TODO: If something else bumps this could cause mega-speed.
                        // If some form of speed update system if multiple things are touching it use that.
                        if (Owner.TryGetComponent(out MovementSpeedModifierComponent? movementSlowdownComponent1))
                        {
                            movementSlowdownComponent1.RefreshMovementSpeedModifiers();
                        }
                        _lastHungerThreshold = _currentHungerThreshold;
                        _actualDecayRate = _baseDecayRate * 0.6f;
                        return;

                    case HungerThreshold.Dead:
                        return;
                    default:
                        Logger.ErrorS("hunger", $"No hunger threshold found for {_currentHungerThreshold}");
                        throw new ArgumentOutOfRangeException($"No hunger threshold found for {_currentHungerThreshold}");
                }
            }
        }

        protected override void Startup()
        {
            base.Startup();
            // Similar functionality to SS13. Should also stagger people going to the chef.
            _currentHunger = _random.Next(
                (int)_hungerThresholds[HungerThreshold.Peckish] + 10,
                (int)_hungerThresholds[HungerThreshold.Okay] - 1);
            _currentHungerThreshold = GetHungerThreshold(_currentHunger);
            _lastHungerThreshold = HungerThreshold.Okay; // TODO: Potentially change this -> Used Okay because no effects.
            HungerThresholdEffect(true);
            Dirty();
        }

        public HungerThreshold GetHungerThreshold(float food)
        {
            HungerThreshold result = HungerThreshold.Dead;
            var value = HungerThresholds[HungerThreshold.Overfed];
            foreach (var threshold in _hungerThresholds)
            {
                if (threshold.Value <= value && threshold.Value >= food)
                {
                    result = threshold.Key;
                    value = threshold.Value;
                }
            }

            return result;
        }

        public void UpdateFood(float amount)
        {
            _currentHunger = Math.Min(_currentHunger + amount, HungerThresholds[HungerThreshold.Overfed]);
        }

        // TODO: If mob is moving increase rate of consumption?
        //  Should use a multiplier as something like a disease would overwrite decay rate.
        public void OnUpdate(float frametime)
        {
            _currentHunger -= frametime * ActualDecayRate;
            UpdateCurrentThreshold();

            if (_currentHungerThreshold != HungerThreshold.Dead)
                return;

            if (!Owner.TryGetComponent(out IDamageableComponent? damageable))
                return;

            if (!Owner.TryGetComponent(out IMobStateComponent? mobState))
                return;

            if (!mobState.IsDead())
            {
                // Recover some health over time
                var damageRecovered = _damageRecoveredPerSecond * frametime;
                _acumulatedDamageRecovery += damageRecovered - ((int) damageRecovered);
                damageable.ChangeDamage(_damageType, (int) -damageRecovered);
                if (_acumulatedDamageRecovery >= 1) {
                    _acumulatedDamageRecovery -= 1;
                    damageable.ChangeDamage(_damageType, -1);
                }
            }
        }

        private void UpdateCurrentThreshold()
        {
            var calculatedHungerThreshold = GetHungerThreshold(_currentHunger);
            // _trySound(calculatedThreshold);
            if (calculatedHungerThreshold != _currentHungerThreshold)
            {
                _currentHungerThreshold = calculatedHungerThreshold;
                HungerThresholdEffect();
                Dirty();
            }
        }

        public void ResetFood()
        {
            _currentHunger = HungerThresholds[HungerThreshold.Okay];
            UpdateCurrentThreshold();
        }

        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new HungerComponentState(_currentHungerThreshold);
        }
    }
}
