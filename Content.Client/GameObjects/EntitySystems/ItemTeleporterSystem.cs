using Content.Client.GameObjects.Components.Movement;
using Content.Shared.GameObjects.Components.Movement;
using Content.Client.Interfaces.GameObjects;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Input;
using Robust.Shared.IoC;

namespace Content.Client.GameObjects.EntitySystems
{
    [UsedImplicitly]
    public class ItemTeleporterSystem : EntitySystem
    {
#pragma warning disable 649
        [Dependency] private readonly IPlayerManager _playerManager;
        [Dependency] private readonly IEyeManager _eyeManager;
        [Dependency] private readonly IInputManager _inputManager;
#pragma warning restore 649
        /// <summary>
        /// This is to be used for item directed teleportation, more or less a blink effect.
        /// </summary>
        private InputSystem _inputSystem;
        private bool _blocked;

        public override void Initialize()
        {
            base.Initialize();

            IoCManager.InjectDependencies(this);
            _inputSystem = EntitySystemManager.GetEntitySystem<InputSystem>();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var state = _inputSystem.CmdStates.GetState(ContentKeyFunctions.UseItemInHand);
            if (state != BoundKeyState.Down)
            {
                _blocked = false;
                return;
            }

            var entity = _playerManager.LocalPlayer.ControlledEntity;
            if (entity == null || !entity.TryGetComponent(out IHandsComponent hands))
            {
                return;
            }

            var held = hands.ActiveHand;
            if (held == null || !held.TryGetComponent(out ClientTeleporterComponent teleporter))
            {
                _blocked = true;
                return;
            }

            if (_blocked)
            {
                return;
            }

            var worldPos = _eyeManager.ScreenToWorld(_inputManager.MouseScreenPosition);

            teleporter.TryClientTeleport(worldPos);
        }
    }
}
