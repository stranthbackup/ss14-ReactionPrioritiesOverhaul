﻿using Content.Shared.Administration.Events;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Console;

namespace Content.Client.Administration.UI.Tabs.PanicBunkerTab;

[GenerateTypedNameReferences]
public sealed partial class PanicBunkerTab : Control
{
    [Dependency] private readonly IConsoleHost _console = default!;

    public PanicBunkerTab()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        DisableAutomaticallyButton.ToolTip = Loc.GetString("admin-ui-panic-bunker-disable-automatically-tooltip");

        MinAccountAge.OnTextEntered += args =>
        {
            if (string.IsNullOrWhiteSpace(args.Text) || !int.TryParse(args.Text, out var minutes))
                return;

            _console.ExecuteCommand($"panicbunker_min_account_age {minutes}");
        };

        MinOverallHours.OnTextEntered += args =>
        {
            if (string.IsNullOrWhiteSpace(args.Text) || !int.TryParse(args.Text, out var hours))
                return;

            _console.ExecuteCommand($"panicbunker_min_overall_hours {hours}");
        };
    }

    public void UpdateStatus(PanicBunkerStatus status)
    {
        EnabledButton.Pressed = status.Enabled;
        EnabledButton.Text = Loc.GetString(status.Enabled
            ? "admin-ui-panic-bunker-enabled"
            : "admin-ui-panic-bunker-disabled"
        );
        EnabledButton.ModulateSelfOverride = status.Enabled ? Color.Red : null;

        DisableAutomaticallyButton.Pressed = status.DisableWithAdmins;
        EnableAutomaticallyButton.Pressed = status.EnableWithoutAdmins;
        CountDeadminnedButton.Pressed = status.CountDeadminnedAdmins;
        ShowReasonButton.Pressed = status.ShowReason;
        MinAccountAge.Text = status.MinAccountAgeHours.ToString();
        MinOverallHours.Text = status.MinOverallHours.ToString();
    }
}
