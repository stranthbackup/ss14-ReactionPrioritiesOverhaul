﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Robust.Client.GameObjects;
using Robust.Shared.IoC;
using System;
using Content.Client.Stylesheets;
using Content.Shared.APC;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using FancyWindow = Content.Client.UserInterface.Controls.FancyWindow;
using Content.Shared.Power;

namespace Content.Client.Power.APC.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class ApcMenu : FancyWindow
    {
        public ApcMenu(ApcBoundUserInterface owner)
        {
            IoCManager.InjectDependencies(this);
            RobustXamlLoader.Load(this);

            EntityView.SetEntity(owner.Owner);
            BreakerButton.OnPressed += _ => owner.BreakerPressed();
        }

        public void UpdateState(ApcBoundInterfaceState state)
        {
            if (BreakerButton != null)
            {
                if (state.HasAccess == false)
                {
                    BreakerButton.Disabled = true;
                    BreakerButton.ToolTip = Loc.GetString("apc-component-insufficient-access");
                }
                else
                {
                    BreakerButton.Disabled = false;
                    BreakerButton.ToolTip = null;
                    BreakerButton.Pressed = state.MainBreaker;
                }
            }

            if (PowerLabel != null)
            {
                PowerLabel.Text = state.Power + " W";
            }

            if (ExternalPowerStateLabel != null)
            {
                PowerUIHelpers.FillExternalPowerLabel(ExternalPowerStateLabel, state.ExternalPower);
            }

            if (ChargeBar != null)
            {
                PowerUIHelpers.FillBatteryChargeProgressBar(ChargeBar, state.Charge);
                var chargePercentage = (state.Charge / ChargeBar.MaxValue);
                ChargePercentage.Text = Loc.GetString("apc-menu-charge-label", ("percent", chargePercentage.ToString("P0")));
            }
        }
    }
}
