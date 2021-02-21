﻿using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.GameObjects.Components.Suspicion;
using Content.Client.GameObjects.EntitySystems;
using Content.Shared.Interfaces;
using Robust.Client.AutoGenerated;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client.UserInterface.Suspicion
{
    [GenerateTypedNameReferences]
    public partial class SuspicionGui : Control
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly IGameTiming _timing = default!;

        private string? _previousRoleName;
        private bool _previousAntagonist;

        public SuspicionGui()
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);

            RoleButton.OnPressed += RoleButtonPressed;
            RoleButton.MinSize = (200, 60);
        }

        private void RoleButtonPressed(ButtonEventArgs obj)
        {
            if (!TryGetComponent(out var role))
            {
                return;
            }

            if (!role.Antagonist ?? false)
            {
                return;
            }

            var allies = string.Join(", ", role.Allies.Select(tuple => tuple.name));
            var message = role.Allies.Count switch
            {
                0 => Loc.GetString("You have no allies"),
                var n => Loc.GetPluralString("Your ally is {0}", "Your allies are {0}", n, allies),
            };

            role.Owner.PopupMessage(message);
        }

        private bool TryGetComponent([NotNullWhen(true)] out SuspicionRoleComponent? suspicion)
        {
            suspicion = default;
            if (_playerManager.LocalPlayer?.ControlledEntity == null)
            {
                return false;
            }

            return _playerManager.LocalPlayer.ControlledEntity.TryGetComponent(out suspicion);
        }

        public void UpdateLabel()
        {
            if (!TryGetComponent(out var suspicion))
            {
                Visible = false;
                return;
            }

            if (suspicion.Role == null || suspicion.Antagonist == null)
            {
                Visible = false;
                return;
            }

            var endTime = EntitySystem.Get<SuspicionEndTimerSystem>().EndTime;
            if (endTime == null)
            {
                TimerLabel.Visible = false;
            }
            else
            {
                var diff = endTime.Value - _timing.CurTime;
                if (diff < TimeSpan.Zero)
                {
                    diff = TimeSpan.Zero;
                }
                TimerLabel.Visible = true;
                TimerLabel.Text = $"{diff:mm\\:ss}";
            }

            if (_previousRoleName == suspicion.Role && _previousAntagonist == suspicion.Antagonist)
            {
                return;
            }

            _previousRoleName = suspicion.Role;
            _previousAntagonist = suspicion.Antagonist.Value;

            var buttonText = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(_previousRoleName);
            buttonText = Loc.GetString(buttonText);

            RoleButton.Text = buttonText;
            RoleButton.ModulateSelfOverride = _previousAntagonist ? Color.Red : Color.Green;

            Visible = true;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            UpdateLabel();
        }
    }
}
