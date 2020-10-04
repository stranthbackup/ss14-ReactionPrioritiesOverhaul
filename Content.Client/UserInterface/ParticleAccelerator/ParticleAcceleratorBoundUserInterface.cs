﻿using Content.Shared.GameObjects.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects.Components.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects.Components.UserInterface;

namespace Content.Client.ParticleAccelerator
{
    public class ParticleAcceleratorBoundUserInterface : BoundUserInterface
    {
        private ParticleAcceleratorControlMenu _menu;

        public ParticleAcceleratorBoundUserInterface([NotNull] ClientUserInterfaceComponent owner, [NotNull] object uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = new ParticleAcceleratorControlMenu(this);
            _menu.OnClose += Close;
            _menu.OpenCentered();
        }

        public void SendDecreaseMessage()
        {
            SendMessage(new ParticleAcceleratorDecreasePowerMessage());
        }

        public void SendIncreaseMessage()
        {
            SendMessage(new ParticleAcceleratorIncreasePowerMessage());
        }

        public void SendToggleMessage()
        {
            SendMessage(new ParticleAcceleratorTogglePowerMessage());
        }

        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (!(message is ParticleAcceleratorDataUpdateMessage dataUpdateMessage)) return;

            _menu.DataUpdate(dataUpdateMessage);
        }
    }
}
