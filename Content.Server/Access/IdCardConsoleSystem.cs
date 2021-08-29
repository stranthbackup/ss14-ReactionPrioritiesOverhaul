using Content.Server.Access.Components;
using Content.Server.Verbs;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.Server.Access
{
    public class IdCardConsoleSystem : EntitySystem 
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<IdCardConsoleComponent, AssembleVerbsEvent>(HandleVerbAssembly);
        }

        private void HandleVerbAssembly(EntityUid uid, IdCardConsoleComponent component, AssembleVerbsEvent args)
        {
            // This system only defines physical interaction verbs
            if (args.Hands == null)
                return;

            // Primary interactions verbs try to insert IDs
            if ((args.Interaction == null || args.Interaction == InteractionType.Primary) &&
                args.Using != null &&
                args.Using.HasComponent<IdCardComponent>())
            {
                // Can we insert a privileged ID? 
                if (component.PrivilegedIDEmpty)
                {
                    Verb verb = new();
                    verb.Act = () => component.InsertIdFromHand(args.User, component.PrivilegedIdContainer, args.Hands);
                    if (args.PrepareGUI)
                    {
                        verb.LocText = "access-insert-privileged-id-verb-get-data-text";
                        verb.IconTexture = "/Textures/Interface/VerbIcons/insert.svg.192dpi.png";
                    }
                    verb.Priority = 1;
                    args.Verbs.Add("IDConsole:InsertPrivilegedID", verb);
                }

                // Can we insert a target ID?
                if (component.TargetIDEmpty)
                {
                    Verb verb = new();
                    verb.Act = () => component.InsertIdFromHand(args.User, component.TargetIdContainer, args.Hands);
                    if (args.PrepareGUI)
                    {
                        verb.LocText = "access-insert-target-id-verb-get-data-text";
                        verb.IconTexture = "/Textures/Interface/VerbIcons/insert.svg.192dpi.png";
                    }
                    verb.Priority = 1;
                    args.Verbs.Add("IDConsole:InsertTargetID", verb);
                }
            }

            // Secondary interactions verbs try to eject IDs
            if ((args.Interaction == null || args.Interaction == InteractionType.Secondary))
            {
                // Can we eject a privileged ID? 
                if (!component.PrivilegedIDEmpty)
                {
                    Verb verb = new();
                    verb.Act = () => component.PutIdInHand(component.PrivilegedIdContainer, args.Hands);
                    if (args.PrepareGUI)
                    {
                        verb.LocText = "access-eject-privileged-id-verb-get-data-text";
                        verb.IconTexture = "/Textures/Interface/VerbIcons/eject.svg.192dpi.png";
                    }
                    verb.Priority = -1;
                    args.Verbs.Add("IDConsole:EjectPrivilegedID", verb);
                }

                // Can we eject a target ID?
                if (!component.TargetIDEmpty)
                {
                    Verb verb = new();
                    verb.Act = () => component.PutIdInHand(component.TargetIdContainer, args.Hands);
                    if (args.PrepareGUI)
                    {
                        verb.LocText = "access-eject-target-id-verb-get-data-text";
                        verb.IconTexture = "/Textures/Interface/VerbIcons/eject.svg.192dpi.png";
                    }
                    args.Verbs.Add("IDConsole:EjectTargetID", verb);
                    verb.Priority = -1;
                }
            }
        }
    }
}
