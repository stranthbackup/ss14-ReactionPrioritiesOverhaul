using System;
using System.Threading.Tasks;
using Content.Server.Interfaces.GameObjects.Components.Items;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Items.RCD
{
    [RegisterComponent]
    public class RCDAmmoComponent : Component, IAfterInteract, IExamine
    {
        public override string Name => "RCDAmmo";

        //How much ammo we refill
        [ViewVariables(VVAccess.ReadWrite)] [DataField("refillAmmo")] private int refillAmmo = 5;

        public void Examine(FormattedMessage message, bool inDetailsRange)
        {
            message.AddMarkup(Loc.GetString("rcd-ammo-component-on-examine-text",("ammo", refillAmmo)));
        }

        async Task<bool> IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            if (eventArgs.Target == null ||
                !eventArgs.Target.TryGetComponent(out RCDComponent? rcdComponent) ||
                !eventArgs.User.TryGetComponent(out IHandsComponent? hands))
            {
                return false;
            }

            if (rcdComponent.MaxAmmo - rcdComponent._ammo < refillAmmo)
            {
                rcdComponent.Owner.PopupMessage(eventArgs.User, Loc.GetString("rcd-ammo-component-after-interact-full-text"));
                return true;
            }

            rcdComponent._ammo = Math.Min(rcdComponent.MaxAmmo, rcdComponent._ammo + refillAmmo);
            rcdComponent.Owner.PopupMessage(eventArgs.User, Loc.GetString("rcd-ammo-component-after-interact-refilled-text"));

            //Deleting a held item causes a lot of errors
            hands.Drop(Owner, false);
            Owner.Delete();
            return true;
        }
    }
}
