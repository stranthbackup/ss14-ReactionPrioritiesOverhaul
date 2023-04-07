﻿using Content.Shared.Fax;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Fax.AdminUI;

[GenerateTypedNameReferences]
public sealed partial class AdminFaxWindow : DefaultWindow
{
    public AdminFaxWindow()
    {
        RobustXamlLoader.Load(this);
    }

    public void Populate(List<AdminFaxEntry> faxes)
    {
        for (var i = 0; i < faxes.Count; i++)
        {
            var fax = faxes[i];
            FaxSelector.AddItem($"{fax.Name} ({fax.Address})", i);
            FaxSelector.SetItemMetadata(i, fax.Uid);
        }
    }
}
