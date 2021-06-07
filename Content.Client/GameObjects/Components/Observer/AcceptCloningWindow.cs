using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;

namespace Content.Client.GameObjects.Components.Observer
{
    public sealed class AcceptCloningWindow : SS14Window
    {
        public readonly Button DenyButton;
        public readonly Button AcceptButton;

        public AcceptCloningWindow()
        {

            Title = Loc.GetString("accept-cloning-window-title");

            Contents.AddChild(new VBoxContainer
            {
                Children =
                {
                    new VBoxContainer
                    {
                        Children =
                        {
                            (new Label()
                            {
                                Text = Loc.GetString("accept-cloning-window-prompt-text-part-1" + "\n" + "accept-cloning-window-prompt-text-part-2")
                            }),
                            new HBoxContainer
                            {
                                Align  = BoxContainer.AlignMode.Center,
                                Children =
                                {
                                    (AcceptButton = new Button
                                    {
                                        Text = Loc.GetString("generic-yes"),
                                    }),

                                    (new Control()
                                    {
                                        MinSize = (20, 0)
                                    }),

                                    (DenyButton = new Button
                                    {
                                        Text = Loc.GetString("generic-no"),
                                    })
                                }
                            },
                        }
                    },
                }
            });
        }
    }
}
