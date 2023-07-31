using Robust.Client.UserInterface.CustomControls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.Labels.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class HandLabelerWindow : DefaultWindow
    {
        public event Action<string>? OnLabelChanged;

        public HandLabelerWindow()
        {
            RobustXamlLoader.Load(this);

            LabelLineEdit.OnTextEntered += e => OnLabelChanged?.Invoke(e.Text);
            LabelLineEdit.OnFocusExit += e => OnLabelChanged?.Invoke(e.Text);
        }

        public void SetCurrentLabel(string label)
        {
            LabelLineEdit.Text = label;
        }
    }
}
