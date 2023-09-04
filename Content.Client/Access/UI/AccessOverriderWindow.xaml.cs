using System.Linq;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using static Content.Shared.Access.Components.AccessOverriderComponent;

namespace Content.Client.Access.UI
{
    [GenerateTypedNameReferences]
    [InjectDependencies]
    public sealed partial class AccessOverriderWindow : DefaultWindow
    {
        [Dependency] private ILogManager _logManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;

        private readonly ISawmill _logMill = default!;
        private readonly AccessOverriderBoundUserInterface _owner;
        private readonly Dictionary<string, Button> _accessButtons = new();

        public AccessOverriderWindow(AccessOverriderBoundUserInterface owner, IPrototypeManager prototypeManager,
            List<string> accessLevels)
        {
            RobustXamlLoader.Load(this);
            IoCManager.InjectDependencies(this);
            _logMill = _logManager.GetSawmill(SharedAccessOverriderSystem.Sawmill);

            _owner = owner;

            foreach (var access in accessLevels)
            {
                if (!prototypeManager.TryIndex<AccessLevelPrototype>(access, out var accessLevel))
                {
                    _logMill.Error($"Unable to find accesslevel for {access}");
                    continue;
                }

                var newButton = new Button
                {
                    Text = GetAccessLevelName(accessLevel),
                    ToggleMode = true,
                };

                AccessLevelGrid.AddChild(newButton);
                _accessButtons.Add(accessLevel.ID, newButton);
                newButton.OnPressed += _ => SubmitData();
            }
        }

        private static string GetAccessLevelName(AccessLevelPrototype prototype)
        {
            if (prototype.Name is { } name)
                return Loc.GetString(name);

            return prototype.ID;
        }

        public void UpdateState(AccessOverriderBoundUserInterfaceState state)
        {
            PrivilegedIdLabel.Text = state.PrivilegedIdName;
            PrivilegedIdButton.Text = state.IsPrivilegedIdPresent
                ? Loc.GetString("access-overrider-window-eject-button")
                : Loc.GetString("access-overrider-window-insert-button");

            TargetNameLabel.Text = state.TargetLabel;
            TargetNameLabel.FontColorOverride = state.TargetLabelColor;

            MissingPrivilegesLabel.Text = "";
            MissingPrivilegesLabel.FontColorOverride = Color.Yellow;

            MissingPrivilegesText.Text = "";
            MissingPrivilegesText.FontColorOverride = Color.Yellow;

            if (state.MissingPrivilegesList != null && state.MissingPrivilegesList.Any())
            {
                List<string> missingPrivileges = new List<string>();

                foreach (string tag in state.MissingPrivilegesList)
                {
                    string privilege = Loc.GetString(_prototypeManager.Index<AccessLevelPrototype>(tag)?.Name ?? "generic-unknown");
                    missingPrivileges.Add(privilege);
                }

                MissingPrivilegesLabel.Text = Loc.GetString("access-overrider-window-missing-privileges");
                MissingPrivilegesText.Text = string.Join(", ", missingPrivileges);
            }

            var interfaceEnabled = state.IsPrivilegedIdPresent && state.IsPrivilegedIdAuthorized;

            foreach (var (accessName, button) in _accessButtons)
            {
                button.Disabled = !interfaceEnabled;
                if (interfaceEnabled)
                {
                    button.Pressed = state.TargetAccessReaderIdAccessList?.Contains(accessName) ?? false;
                    button.Disabled = (!state.AllowedModifyAccessList?.Contains(accessName)) ?? true;
                }
            }
        }

        private void SubmitData()
        {
            _owner.SubmitData(

                // Iterate over the buttons dictionary, filter by `Pressed`, only get key from the key/value pair
                _accessButtons.Where(x => x.Value.Pressed).Select(x => x.Key).ToList());
        }
    }
}
