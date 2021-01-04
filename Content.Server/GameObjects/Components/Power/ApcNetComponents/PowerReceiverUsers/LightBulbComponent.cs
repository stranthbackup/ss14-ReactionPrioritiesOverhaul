﻿using System;
using Content.Shared.Audio;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Power.ApcNetComponents.PowerReceiverUsers
{
    public enum LightBulbState
    {
        Normal,
        Broken,
        Burned,
    }

    public enum LightBulbType
    {
        Bulb,
        Tube,
    }

    /// <summary>
    ///     Component that represents a light bulb. Can be broken, or burned, which turns them mostly useless.
    /// </summary>
    [RegisterComponent]
    public class LightBulbComponent : Component, ILand
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        /// <summary>
        ///     Invoked whenever the state of the light bulb changes.
        /// </summary>
        public event EventHandler<EventArgs> OnLightBulbStateChange;
        public event EventHandler<EventArgs> OnLightColorChange;

        [YamlField("color")]
        private Color _color = Color.White;

        [ViewVariables(VVAccess.ReadWrite)] public Color Color
        {
            get { return _color; }
            set
            {
                _color = value;
                OnLightColorChange?.Invoke(this, null);
                UpdateColor();
            }
        }

        public override string Name => "LightBulb";

        [YamlField("bulb")]
        public LightBulbType Type = LightBulbType.Tube;

        [YamlField("BurningTemperature")]
        private int _burningTemperature = 1400;
        public int BurningTemperature => _burningTemperature;

        [YamlField("PowerUse")]
        private int _powerUse = 40;
        public int PowerUse => _powerUse;

        /// <summary>
        ///     The current state of the light bulb. Invokes the OnLightBulbStateChange event when set.
        ///     It also updates the bulb's sprite accordingly.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)] public LightBulbState State
        {
            get { return _state; }
            set
            {
                var sprite = Owner.GetComponent<SpriteComponent>();
                OnLightBulbStateChange?.Invoke(this, EventArgs.Empty);
                _state = value;
                switch (value)
                {
                    case LightBulbState.Normal:
                        sprite.LayerSetState(0, "normal");
                        break;
                    case LightBulbState.Broken:
                        sprite.LayerSetState(0, "broken");
                        break;
                    case LightBulbState.Burned:
                        sprite.LayerSetState(0, "burned");
                        break;
                }
            }
        }

        private LightBulbState _state = LightBulbState.Normal;

        public void UpdateColor()
        {
            if (!Owner.TryGetComponent(out SpriteComponent sprite))
            {
                return;
            }

            sprite.Color = Color;
        }

        public override void Initialize()
        {
            base.Initialize();
            UpdateColor();
        }

        public void Land(LandEventArgs eventArgs)
        {
            if (State == LightBulbState.Broken)
                return;

            var soundCollection = _prototypeManager.Index<SoundCollectionPrototype>("glassbreak");
            var file = _random.Pick(soundCollection.PickFiles);

            EntitySystem.Get<AudioSystem>().PlayFromEntity(file, Owner);

            State = LightBulbState.Broken;
        }
    }
}
