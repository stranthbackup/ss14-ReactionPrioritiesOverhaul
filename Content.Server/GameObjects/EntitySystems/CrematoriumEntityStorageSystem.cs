using Content.Server.GameObjects.Components.GUI;
using Content.Server.GameObjects.Components.Morgue;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class CrematoriumEntityStorageSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<HandsComponent, DownEvent>(HandleDropItems);
        }

        private void HandleDropItems(EntityUid uid, HandsComponent component, DownEvent args)
        {
            // So the directed event is only raised on the entity itself so we need to check container on a downed entity I supposed
            if (!component.Owner.TryGetContainerMan(out var conMan) ||
                !conMan.Owner.HasComponent<CrematoriumEntityStorageComponent>()) return;

            // Yes this is kinda shit but when the other caller to DropAllItemsInHands is fixed we can fix this.
            Get<SharedHandsSystem>().DropAllItemsInHands(EntityManager.GetEntity(uid));
        }
    }
}
