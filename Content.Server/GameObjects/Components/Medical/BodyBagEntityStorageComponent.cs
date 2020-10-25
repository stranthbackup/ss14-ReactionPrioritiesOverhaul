﻿using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Server.GameObjects.Components.Paper;
using Content.Server.GameObjects.EntitySystems;
using Content.Server.Interfaces.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Body;
using Content.Shared.GameObjects.Components.Rotation;
using Content.Shared.GameObjects.Components.Storage;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Verbs;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Server.GameObjects.Components.Container;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Content.Server.GameObjects.Components.Medical
{
    [RegisterComponent]
    [ComponentReference(typeof(EntityStorageComponent))]
    [ComponentReference(typeof(IActivate))]
    [ComponentReference(typeof(IStorageComponent))]
    public class BodyBagEntityStorageComponent : EntityStorageComponent, IExamine, IInteractUsing
    {
        public override string Name => "BodyBagEntityStorage";
        private IEntityQuery _entityQuery;
        [ViewVariables] private AppearanceComponent _appearance;
        [ViewVariables] public ContainerSlot LabelContainer { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            _entityQuery = new IntersectingEntityQuery(Owner);
            _appearance = Owner.GetComponent<AppearanceComponent>();
            _appearance.SetData(BodyBagVisuals.Label, false);
            LabelContainer = ContainerManagerComponent.Ensure<ContainerSlot>("body_bag_label", Owner, out _);
        }

        protected override bool CanClose(IEntity user)
        {
            foreach (var entity in Owner.EntityManager.GetEntities(_entityQuery))
            {
                if (entity == Owner) continue;
                if (entity.HasComponent<IBody>() && !EntitySystem.Get<StandingStateSystem>().IsDown(entity)) return false;
            }
            return base.CanClose(user);
        }

        void IExamine.Examine(FormattedMessage message, bool inDetailsRange)
        {
            if (inDetailsRange)
            {
                if (LabelContainer.ContainedEntity != null && LabelContainer.ContainedEntity.TryGetComponent<PaperComponent>(out var paper))
                {
                    message.AddText(Loc.GetString("The label reads: {0}", paper.Content));
                }
            }
        }

        async Task<bool> IInteractUsing.InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (LabelContainer.ContainedEntity != null)
            {
                Owner.PopupMessage(eventArgs.User, Loc.GetString("There's already a label attached."));
                return false;
            }

            var handsComponent = eventArgs.User.GetComponent<IHandsComponent>();
            if (!handsComponent.Drop(eventArgs.Using, LabelContainer))
            {
                return false;
            }

            _appearance.SetData(BodyBagVisuals.Label, true);

            Owner.PopupMessage(eventArgs.User, Loc.GetString("You attach {0:theName} to the body bag.", eventArgs.Using));
            return true;
        }

        public void RemoveLabel(IEntity user)
        {
            if (user.TryGetComponent(out HandsComponent hands))
            {
                hands.PutInHandOrDrop(LabelContainer.ContainedEntity.GetComponent<ItemComponent>());
                _appearance.SetData(BodyBagVisuals.Label, false);
            }
            else if (LabelContainer.Remove(LabelContainer.ContainedEntity))
            {
                LabelContainer.ContainedEntity.Transform.Coordinates = Owner.Transform.Coordinates;
                _appearance.SetData(BodyBagVisuals.Label, false);
            }
        }


        [Verb]
        private sealed class RemoveLabelVerb : Verb<BodyBagEntityStorageComponent>
        {
            protected override void GetData(IEntity user, BodyBagEntityStorageComponent component, VerbData data)
            {
                if (!ActionBlockerSystem.CanInteract(user) || component.LabelContainer.ContainedEntity == null)
                {
                    data.Visibility = VerbVisibility.Invisible;
                    return;
                }

                data.Text = Loc.GetString("Remove label");
            }

            /// <inheritdoc />
            protected override void Activate(IEntity user, BodyBagEntityStorageComponent component)
            {
                component.RemoveLabel(user);
            }
        }
    }
}
