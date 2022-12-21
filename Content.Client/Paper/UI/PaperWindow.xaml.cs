using Content.Shared.Paper;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;

namespace Content.Client.Paper.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class PaperWindow : DefaultWindow
    {
        public PaperWindow()
        {
            RobustXamlLoader.Load(this);
        }

        public void Populate(SharedPaperComponent.PaperBoundUserInterfaceState state)
        {
            bool isEditing = state.Mode == SharedPaperComponent.PaperAction.Write;
            Input.Visible = isEditing;

            var msg = new FormattedMessage();
            msg.AddMarkupPermissive(state.Text);
            Label.SetMessage(msg);

            BlankPaperIndicator.Visible = !isEditing && state.Text.Length == 0;
            BlankPaperPlaceholder.Visible = !BlankPaperIndicator.Visible;

            StampDisplay.RemoveAllChildren();
            foreach(var stamper in state.StampedBy)
            {
                StampDisplay.AddChild(new StampWidget{ Stamper = stamper });
            }
        }
    }
}
