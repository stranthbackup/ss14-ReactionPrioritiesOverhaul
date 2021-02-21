﻿using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Chat
{
    public class ChatBox : Control
    {
        public delegate void TextSubmitHandler(ChatBox chatBox, string text);

        public delegate void FilterToggledHandler(ChatBox chatBox, BaseButton.ButtonToggledEventArgs e);

        public HistoryLineEdit Input { get; private set; }
        public OutputPanel Contents { get; }

        // Buttons for filtering
        public Button AllButton { get; }
        public Button LocalButton { get; }
        public Button OOCButton { get; }
        public Button AdminButton { get; }
        public Button DeadButton { get; }

        /// <summary>
        ///     Default formatting string for the ClientChatConsole.
        /// </summary>
        public string DefaultChatFormat { get; set; } = string.Empty;

        public bool ReleaseFocusOnEnter { get; set; } = true;

        public bool ClearOnEnter { get; set; } = true;

        public ChatBox()
        {
            MouseFilter = MouseFilterMode.Stop;

            var outerVBox = new VBoxContainer();

            var panelContainer = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat {BackgroundColor = Color.FromHex("#25252aaa")},
                VerticalExpand = true
            };
            var vBox = new VBoxContainer();
            panelContainer.AddChild(vBox);
            var hBox = new HBoxContainer();

            outerVBox.AddChild(panelContainer);
            outerVBox.AddChild(hBox);

            Contents = new OutputPanel {Margin = new Thickness(4, 0), VerticalExpand = true};
            vBox.AddChild(Contents);

            Input = new HistoryLineEdit();
            Input.OnKeyBindDown += InputKeyBindDown;
            Input.OnTextEntered += Input_OnTextEntered;
            vBox.AddChild(Input);

            AllButton = new Button
            {
                Text = Loc.GetString("All"),
                Name = "ALL",
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Right,
                ToggleMode = true,
            };

            LocalButton = new Button
            {
                Text = Loc.GetString("Local"),
                Name = "Local",
                ToggleMode = true,
            };

            OOCButton = new Button
            {
                Text = Loc.GetString("OOC"),
                Name = "OOC",
                ToggleMode = true,
            };

            AdminButton = new Button
            {
                Text = Loc.GetString("Admin"),
                Name = "Admin",
                ToggleMode = true,
                Visible = false
            };

            DeadButton = new Button
            {
                Text = Loc.GetString("Dead"),
                Name = "Dead",
                ToggleMode = true,
                Visible = false
            };

            AllButton.OnToggled += OnFilterToggled;
            LocalButton.OnToggled += OnFilterToggled;
            OOCButton.OnToggled += OnFilterToggled;
            AdminButton.OnToggled += OnFilterToggled;
            DeadButton.OnToggled += OnFilterToggled;

            hBox.AddChild(AllButton);
            hBox.AddChild(LocalButton);
            hBox.AddChild(DeadButton);
            hBox.AddChild(OOCButton);
            hBox.AddChild(AdminButton);

            AddChild(outerVBox);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);

            if (!args.CanFocus)
            {
                return;
            }

            Input.GrabKeyboardFocus();
        }

        private void InputKeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function == EngineKeyFunctions.TextReleaseFocus)
            {
                Input.ReleaseKeyboardFocus();
                args.Handle();
                return;
            }
        }

        public event TextSubmitHandler? TextSubmitted;

        public event FilterToggledHandler? FilterToggled;

        public void AddLine(string message, ChatChannel channel, Color color)
        {
            if (Disposed)
            {
                return;
            }

            var formatted = new FormattedMessage(3);
            formatted.PushColor(color);
            formatted.AddText(message);
            formatted.Pop();
            Contents.AddMessage(formatted);
        }

        private void Input_OnTextEntered(LineEdit.LineEditEventArgs args)
        {
            // We set it there to true so it's set to false by TextSubmitted.Invoke if necessary
            ClearOnEnter = true;

            if (!string.IsNullOrWhiteSpace(args.Text))
            {
                TextSubmitted?.Invoke(this, args.Text);
            }

            if (ClearOnEnter)
            {
                Input.Clear();
            }

            if (ReleaseFocusOnEnter)
            {
                Input.ReleaseKeyboardFocus();
            }
        }

        private void OnFilterToggled(BaseButton.ButtonToggledEventArgs args)
        {
            FilterToggled?.Invoke(this, args);
        }
    }
}
