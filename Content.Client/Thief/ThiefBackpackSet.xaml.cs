using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Content.Shared.Thief;
using Robust.Client.GameObjects;

namespace Content.Client.Thief;

[GenerateTypedNameReferences]
public sealed partial class ThiefBackpackSet : Control
{

    public ThiefBackpackSet(ThiefBackpackSetInfo set, SpriteSystem spriteSystem)
    {
        RobustXamlLoader.Load(this);

        Icon.Texture = spriteSystem.Frame0(set.Sprite);
        SetName.Text = Loc.GetString(set.Name);
        SetDescription.Text = Loc.GetString(set.Description);
        SetButton.Text = Loc.GetString(set.Selected ? "thief-backpack-button-deselect" : "thief-backpack-button-select");
    }
}
