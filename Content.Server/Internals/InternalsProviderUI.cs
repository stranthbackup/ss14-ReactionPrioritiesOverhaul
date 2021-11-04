using Content.Server.Atmos.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Content.Shared.Hands.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.IoC;
using Content.Shared.Interaction.Helpers;
using Content.Shared.ActionBlocker;
using Content.Server.Stack;
using Content.Server.Tools;
using Content.Server.Tools.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using System.Threading.Tasks;
using Content.Shared.Item;
using Robust.Shared.Map;
using Robust.Shared.ViewVariables;
using Robust.Server.Player;
using Content.Server.UserInterface;
using Content.Shared.Atmos.Components;
using static Content.Server.Atmos.Components.GasTankComponent;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;

namespace Content.Server.Internals
{
    [UsedImplicitly]
    public class InternalsProviderUISystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<InternalsProviderUIComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<InternalsProviderUIComponent, ActivateInWorldEvent>(OnActivate);
            SubscribeLocalEvent<InternalsProviderUIComponent, UseInHandEvent>(OnUse);
            SubscribeLocalEvent<InternalsProviderUIComponent, InteractHandEvent>(OnInteract);
            SubscribeLocalEvent<InternalsProviderUIComponent, GasTankPressureDeficitEvent>(OnTankPressureChange);
        }

        public void OnStartup(EntityUid uid, InternalsProviderUIComponent component, ComponentStartup args)
        {
            component.UserInterface = component.Owner.GetUIOrNull(SharedGasTankUiKey.Key);
            if (component.UserInterface != null)
            {
                component.UserInterface.OnReceiveMessage += (msg) => UserInterfaceOnOnReceiveMessage(component, msg);
            }
        }

        public void OnActivate(EntityUid uid, InternalsProviderUIComponent component, ActivateInWorldEvent args)
        {
            if (!args.User.TryGetComponent(out ActorComponent? actor)) return;
            OpenInterface(component, actor.PlayerSession);
        }
        public void OnUse(EntityUid uid, InternalsProviderUIComponent component, UseInHandEvent args)
        {
            if (!args.User.TryGetComponent(out ActorComponent? actor)) return;
            OpenInterface(component, actor.PlayerSession);
        }

        public void OnInteract(EntityUid uid, InternalsProviderUIComponent component, InteractHandEvent args)
        {
            if (!args.User.TryGetComponent(out ActorComponent? actor)) return;
            OpenInterface(component, actor.PlayerSession);
        }

        public void OnTankPressureChange(EntityUid uid, InternalsProviderUIComponent component, GasTankPressureDeficitEvent args)
        {
            UpdateUserInterface(component, true);
        }

        public void OpenInterface(InternalsProviderUIComponent component, IPlayerSession session)
        {
            component.UserInterface?.Open(session);
            UpdateUserInterface(component, true);
        }

        public void UpdateUserInterface(InternalsProviderUIComponent component, bool initialUpdate = false)
        {
            var provider = component.Owner.GetComponent<InternalsProviderComponent>();
            var internals = provider.Internals;

            var gasTank = component.Owner.GetComponent<GasTankComponent>();

            component.UserInterface?.SetState(
                new GasTankBoundUserInterfaceState
                {
                    TankPressure = gasTank.Air?.Pressure ?? 0,
                    OutputPressure = initialUpdate ? gasTank.OutputPressure : null,
                    InternalsConnected = internals != null,
                    CanConnectInternals = true // todo - fix?
                }
            );
        }

        private void UserInterfaceOnOnReceiveMessage(InternalsProviderUIComponent component, ServerBoundUserInterfaceMessage message)
        {
            switch (message.Message)
            {
                case GasTankSetPressureMessage msg:
                    var gasTank = component.Owner.GetComponent<GasTankComponent>();
                    gasTank.OutputPressure = msg.Pressure;
                    break;
                case GasTankToggleInternalsMessage _:
                    RaiseLocalEvent(component.Owner.Uid, new ToggleInternalsEvent());
                    break;
            }
        }
    }

    [RegisterComponent]
    public class InternalsProviderUIComponent : Component
    {
        public override string Name => "InternalsProviderUI";

        [ViewVariables]
        public BoundUserInterface? UserInterface;
    }
}
