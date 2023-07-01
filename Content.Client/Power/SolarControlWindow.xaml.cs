using System;
using System.Numerics;
using Content.Client.Computer;
using Content.Shared.Solar;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.Power
{
    [GenerateTypedNameReferences]
    public sealed partial class SolarControlWindow : DefaultWindow, IComputerWindow<SolarControlConsoleBoundInterfaceState>
    {
        private SolarControlConsoleBoundInterfaceState _lastState = new(0, 0, 0, 0);

        public SolarControlWindow()
        {
            RobustXamlLoader.Load(this);
        }

        public void SetupComputerWindow(ComputerBoundUserInterfaceBase cb)
        {
            PanelRotation.OnTextEntered += text => {
                double value;
                if (!double.TryParse((string?) text.Text, out value)) return;

                SolarControlConsoleAdjustMessage msg = new()
                {
                    Rotation = Angle.FromDegrees(value), AngularVelocity = _lastState.AngularVelocity,
                };

                cb.SendMessage(msg);

                // Predict this...
                _lastState.Rotation = msg.Rotation;
                NotARadar.UpdateState(_lastState);
            };
            PanelVelocity.OnTextEntered += text => {
                double value;
                if (!double.TryParse((string?) text.Text, out value)) return;

                SolarControlConsoleAdjustMessage msg = new()
                {
                    Rotation = NotARadar.PredictedPanelRotation, AngularVelocity = Angle.FromDegrees(value / 60),
                };

                cb.SendMessage(msg);

                // Predict this...
                _lastState.Rotation = NotARadar.PredictedPanelRotation;
                _lastState.AngularVelocity = msg.AngularVelocity;
                NotARadar.UpdateState(_lastState);
            };
        }

        private string FormatAngle(Angle d)
        {
            return d.Degrees.ToString("F1");
        }

        // The idea behind this is to prevent every update from the server
        //  breaking the textfield.
        private void UpdateField(LineEdit field, string newValue)
        {
            if (!field.HasKeyboardFocus())
            {
                field.Text = newValue;
            }
        }

        public void UpdateState(SolarControlConsoleBoundInterfaceState scc)
        {
            _lastState = scc;
            NotARadar.UpdateState(scc);
            OutputPower.Text = ((int) MathF.Floor(scc.OutputPower)).ToString();
            SunAngle.Text = FormatAngle(scc.TowardsSun);
            UpdateField(PanelRotation, FormatAngle(scc.Rotation));
            UpdateField(PanelVelocity, FormatAngle(scc.AngularVelocity * 60));
        }

    }

    public sealed class SolarControlNotARadar : Control
    {
        // This is used for client-side prediction of the panel rotation.
        // This makes the display feel a lot smoother.
        private IGameTiming _gameTiming = IoCManager.Resolve<IGameTiming>();

        private SolarControlConsoleBoundInterfaceState _lastState = new(0, 0, 0, 0);

        private TimeSpan _lastStateTime = TimeSpan.Zero;

        public const int StandardSizeFull = 290;
        public const int StandardRadiusCircle = 140;
        public int SizeFull => (int) (StandardSizeFull * UIScale);
        public int RadiusCircle => (int) (StandardRadiusCircle * UIScale);

        public SolarControlNotARadar()
        {
            MinSize = new Vector2(SizeFull, SizeFull);
        }

        public void UpdateState(SolarControlConsoleBoundInterfaceState ls)
        {
            _lastState = ls;
            _lastStateTime = _gameTiming.CurTime;
        }

        public Angle PredictedPanelRotation => _lastState.Rotation + (_lastState.AngularVelocity * ((_gameTiming.CurTime - _lastStateTime).TotalSeconds));

        protected override void Draw(DrawingHandleScreen handle)
        {
            var point = SizeFull / 2;
            var fakeAA = new Color(0.08f, 0.08f, 0.08f);
            var gridLines = new Color(0.08f, 0.08f, 0.08f);
            var panelExtentCutback = 4;
            var gridLinesRadial = 8;
            var gridLinesEquatorial = 8;

            // Draw base
            handle.DrawCircle((point, point), RadiusCircle + 1, fakeAA);
            handle.DrawCircle((point, point), RadiusCircle, Color.Black);

            // Draw grid lines
            for (var i = 0; i < gridLinesEquatorial; i++)
            {
                handle.DrawCircle((point, point), (RadiusCircle / gridLinesEquatorial) * i, gridLines, false);
            }

            for (var i = 0; i < gridLinesRadial; i++)
            {
                Angle angle = (Math.PI / gridLinesRadial) * i;
                var aExtent = angle.ToVec() * RadiusCircle;
                handle.DrawLine((point, point) - aExtent, (point, point) + aExtent, gridLines);
            }

            // The rotations need to be adjusted because Y is inverted in Robust (like BYOND)
            Vector2 rotMul = (1, -1);
            // Hotfix corrections I don't understand
            Angle rotOfs = new Angle(Math.PI * -0.5);

            Angle predictedPanelRotation = PredictedPanelRotation;

            var extent = (predictedPanelRotation + rotOfs).ToVec() * rotMul * RadiusCircle;
            Vector2 extentOrtho = (extent.Y, -extent.X);
            handle.DrawLine((point, point) - extentOrtho, (point, point) + extentOrtho, Color.White);
            handle.DrawLine((point, point) + (extent / panelExtentCutback), (point, point) + extent - (extent / panelExtentCutback), Color.DarkGray);

            var sunExtent = (_lastState.TowardsSun + rotOfs).ToVec() * rotMul * RadiusCircle;
            handle.DrawLine((point, point) + sunExtent, (point, point), Color.Yellow);
        }
    }

    [UsedImplicitly]
    public sealed class SolarControlConsoleBoundUserInterface : ComputerBoundUserInterface<SolarControlWindow, SolarControlConsoleBoundInterfaceState>
    {
        public SolarControlConsoleBoundUserInterface(ClientUserInterfaceComponent owner, Enum uiKey) : base(owner, uiKey) {}
    }
}
