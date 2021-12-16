﻿using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;
using Robust.Shared.Utility.Markup;

namespace Content.Client.Info;

[GenerateTypedNameReferences]
public partial class InfoSection : BoxContainer
{
    public InfoSection(string title, string text, bool markup = false)
    {
        RobustXamlLoader.Load(this);

        TitleLabel.Text = title;

        if (markup)
            Content.SetMessage(Basic.RenderMarkup(text.Trim()));
        else
            Content.SetMessage(text);
    }
}
