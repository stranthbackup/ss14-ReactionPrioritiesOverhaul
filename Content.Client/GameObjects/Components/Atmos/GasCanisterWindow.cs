using System.Collections.Generic;
using System.Linq;
using Content.Shared.GameObjects.Components.Atmos;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.Components.Atmos
{
    /// <summary>
    /// Client-side UI used to control a <see cref="SharedGasCanisterComponent"/>
    /// </summary>
    public class GasCanisterWindow : SS14Window
    {
        private readonly Label _pressure;
        private readonly Label _releasePressure;

        public readonly CheckButton ToggleValve;
        public readonly LineEdit LabelInput;
        public readonly Button EditLabelBtn;
        public string OldLabel { get; set; } = "";

        public bool LabelInputEditable {
            get => LabelInput.Editable;
            set {
                LabelInput.Editable = value;
                EditLabelBtn.Text = value ? Loc.GetString("gas-canister-window-ok-text") : Loc.GetString("gas-canister-window-edit-text");
            }
        }

        public List<ReleasePressureButton> ReleasePressureButtons { get; private set; }

        public GasCanisterWindow()
        {
            SetSize = MinSize = (450, 200);
            HBoxContainer releasePressureButtons;

            Contents.AddChild(new VBoxContainer
            {
                Children =
                {
                    new VBoxContainer
                        {
                            Children =
                            {
                                new HBoxContainer()
                                    {
                                        Children =
                                        {
                                            new Label(){ Text = $"{Loc.GetString("gas-canister-window-label-label")} " },
                                            (LabelInput = new LineEdit() { Text = Name ?? "", Editable = false,
                                                MinSize = new Vector2(200, 30)}),
                                            (EditLabelBtn = new Button()),
                                        }
                                    },
                                new HBoxContainer
                                    {
                                        Children =
                                        {
                                            new Label {Text = $"{Loc.GetString("gas-canister-window-pressure-label")} "},
                                            (_pressure = new Label())
                                        }
                                    },
                                new VBoxContainer()
                                {
                                    Children =
                                    {
                                        new HBoxContainer()
                                        {
                                            Children =
                                            {
                                                new Label() {Text = $"{Loc.GetString("gas-canister-window-release-pressure-label")} "},
                                                (_releasePressure = new Label())
                                            }
                                        },
                                        (releasePressureButtons = new HBoxContainer()
                                        {
                                            Children =
                                            {
                                                new ReleasePressureButton() {PressureChange = -50},
                                                new ReleasePressureButton() {PressureChange = -10},
                                                new ReleasePressureButton() {PressureChange = -1},
                                                new ReleasePressureButton() {PressureChange = -0.1f},
                                                new ReleasePressureButton() {PressureChange = 0.1f},
                                                new ReleasePressureButton() {PressureChange = 1},
                                                new ReleasePressureButton() {PressureChange = 10},
                                                new ReleasePressureButton() {PressureChange = 50}
                                            }
                                        })
                                    }
                                },
                                new HBoxContainer()
                                {
                                    Children =
                                    {
                                        new Label { Text = $"{Loc.GetString("gas-canister-window-valve-label")} " },
                                        (ToggleValve = new CheckButton() { Text = Loc.GetString("gas-canister-window-valve-closed-text") })
                                    }
                                }
                            },
                        }
                }
            });

            // Create the release pressure buttons list
            ReleasePressureButtons = new List<ReleasePressureButton>();
            foreach (var control in releasePressureButtons.Children.ToList())
            {
                var btn = (ReleasePressureButton) control;
                ReleasePressureButtons.Add(btn);
            }

            // Reset the editable label
            LabelInputEditable = false;
        }

        /// <summary>
        /// Update the UI based on <see cref="GasCanisterBoundUserInterfaceState"/>
        /// </summary>
        /// <param name="state">The state the UI should reflect</param>
        public void UpdateState(GasCanisterBoundUserInterfaceState state)
        {
            _pressure.Text = Loc.GetString("gas-canister-window-pressure-format-text", ("pressure", state.Volume));
            _releasePressure.Text = Loc.GetString("gas-canister-window-pressure-format-text", ("pressure", state.ReleasePressure));

            // Update the canister label
            OldLabel = LabelInput.Text;
            LabelInput.Text = state.Label;
            Title = state.Label;

            // Reset the editable label
            LabelInputEditable = false;

            ToggleValve.Pressed = state.ValveOpened;
            if (ToggleValve.Pressed)
            {
                ToggleValve.Text = Loc.GetString("gas-canister-window-valve-open-text");
            }
            else
            {
                ToggleValve.Text = Loc.GetString("gas-canister-window-valve-closed-text");
            }
        }
    }


    /// <summary>
    /// Special button class which stores a numerical value and has it as a label
    /// </summary>
    public class ReleasePressureButton : Button
    {
        public float PressureChange
        {
            get { return _pressureChange; }
            set
            {
                _pressureChange = value;
                Text = (value >= 0) ? ("+" + value) : value.ToString();
            }
        }

        private float _pressureChange;

        public ReleasePressureButton() : base() {}
    }
}
