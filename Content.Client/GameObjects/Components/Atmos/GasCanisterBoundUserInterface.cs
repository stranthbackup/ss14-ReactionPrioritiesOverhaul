﻿using Content.Shared.GameObjects.Components.Atmos;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Client.GameObjects.Components.Atmos
{
    /// <summary>
    /// Initializes a <see cref="GasCanisterWindow"/> and updates it when new server messages are received.
    /// </summary>
    [UsedImplicitly]
    public class GasCanisterBoundUserInterface : BoundUserInterface
    {

        private GasCanisterWindow? _window;

        public GasCanisterBoundUserInterface(ClientUserInterfaceComponent owner, object uiKey) : base(owner, uiKey)
        {

        }

        protected override void Open()
        {
            base.Open();

            _window = new GasCanisterWindow();

            _window.OpenCentered();
            _window.OnClose += Close;
        }

        /// <summary>
        /// Update the UI state based on server-sent info
        /// </summary>
        /// <param name="state"></param>
        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not GasCanisterBoundUserInterfaceState cast)
            {
                return;
            }

            _window?.UpdateState(cast);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            _window?.Dispose();
        }
    }
}
