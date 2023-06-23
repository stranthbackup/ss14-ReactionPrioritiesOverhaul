using Content.Client.Computer;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Gateway;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.Gateway.UI;

[GenerateTypedNameReferences]
public sealed partial class GatewayWindow : FancyWindow,
    IComputerWindow<EmergencyConsoleBoundUserInterfaceState>
{
    private readonly IGameTiming _timing;

    public event Action<EntityUid>? OpenPortal;
    private List<(EntityUid, string, TimeSpan, bool)> _destinations = default!;
    private EntityUid? _current;
    private TimeSpan _nextClose;
    private TimeSpan _lastOpen;
    private List<Label> _readyLabels = default!;
    private List<Button> _openButtons = default!;

    public GatewayWindow()
    {
        RobustXamlLoader.Load(this);
        _timing = IoCManager.Resolve<IGameTiming>();
    }

    public void UpdateState(GatewayBoundUserInterfaceState state)
    {
        _destinations = state.Destinations;
        _current = state.Current;
        _nextClose = state.NextClose;
        _lastOpen = state.LastOpen;

        Container.DisposeAllChildren();
        _readyLabels = new List<Label>(_destinations.Count);
        _openButtons = new List<Button>(_destinations.Count);

        if (_destinations.Count == 0)
        {
            Container.AddChild(new BoxContainer()
            {
                HorizontalExpand = true,
                VerticalExpand = true,
                Children =
                {
                    new Label()
                    {
                        Text = Loc.GetString("gateway-window-no-destinations"),
                        HorizontalAlignment = HAlignment.Center
                    }
                }
            });
            return;
        }

        var now = _timing.CurTime;
        foreach (var dest in _destinations)
        {
            var uid = dest.Item1;
            var name = dest.Item2;
            var nextReady = dest.Item3;
            var busy = dest.Item4;

            var box = new BoxContainer()
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal
            };

            box.AddChild(new Label()
            {
                Text = name
            });

            var readyLabel = new Label
            {
                Text = ReadyText(now, nextReady),
                Margin = new Thickness(10f, 0f, 0f, 0f)
            };
            _readyLabels.Add(readyLabel);
            box.AddChild(readyLabel);

            var openButton = new Button()
            {
                Text = Loc.GetString("gateway-window-open-portal"),
                Pressed = uid == _current,
                ToggleMode = true,
                Disabled = _current != null || busy || now < nextReady
            };

            openButton.OnPressed += args =>
            {
                OpenPortal?.Invoke(uid);
            };

            if (uid == state.Current)
            {
                openButton.AddStyleClass(StyleBase.ButtonCaution);
            }

            _openButtons.Add(openButton);
            box.AddChild(new BoxContainer()
            {
                HorizontalExpand = true,
                Align = BoxContainer.AlignMode.End,
                Children =
                {
                    openButton
                }
            });

            Container.AddChild(new PanelContainer()
            {
                PanelOverride = new StyleBoxFlat(new Color(30, 30, 34)),
                Margin = new Thickness(10f, 5f),
                Children =
                {
                    box
                }
            });
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.CurTime;

        // if its not going to close then show it as empty
        if (_current == null)
        {
            NextCloseBar.Value = 0f;
            NextCloseText.Text = "00:00";
        }
        else
        {
            var remaining = _nextClose - _timing.CurTime;
            if (remaining < TimeSpan.Zero)
            {
                NextCloseBar.Value = 1f;
                NextCloseText.Text = "00:00";
            }
            else
            {
                var openTime = _nextClose - _lastOpen;
                NextCloseBar.Value = 1f - (float) (remaining / openTime);
                NextCloseText.Text = $"{remaining.Minutes:00}:{remaining.Seconds:00}";
            }
        }

        for (var i = 0; i < _destinations.Count; i++)
        {
            var dest = _destinations[i];
            var nextReady = dest.Item3;
            var busy = dest.Item4;
            _readyLabels[i].Text = ReadyText(now, nextReady);
            _openButtons[i].Disabled = _current != null || busy || now < nextReady;
        }
    }

    private string ReadyText(TimeSpan now, TimeSpan nextReady)
    {
        if (now < nextReady)
        {
            return Loc.GetString("gateway-window-ready-in", ("time", nextReady - now));
        }

        return Loc.GetString("gateway-window-ready");
    }
}
