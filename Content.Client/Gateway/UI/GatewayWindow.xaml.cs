using Content.Client.Computer;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Gateway;
using Content.Shared.Shuttles.BUIStates;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.Gateway.UI;

[GenerateTypedNameReferences]
public sealed partial class GatewayWindow : FancyWindow,
    IComputerWindow<EmergencyConsoleBoundUserInterfaceState>
{
    private readonly IGameTiming _timing;

    public event Action<NetEntity>? OpenPortal;
    private List<GatewayDestinationData> _destinations = default!;
    private NetEntity? _current;
    private TimeSpan _lastOpen;
    private TimeSpan _nextReady;
    private List<Label> _readyLabels = default!;
    private List<Button> _openButtons = default!;

    public GatewayWindow()
    {
        RobustXamlLoader.Load(this);
        var dependencies = IoCManager.Instance!;
        _timing = dependencies.Resolve<IGameTiming>();
    }

    public void UpdateState(GatewayBoundUserInterfaceState state)
    {
        _destinations = state.Destinations;
        _current = state.Current;
        _nextReady = state.NextReady;
        _lastOpen = state.NextReady;

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
            var ent = dest.Entity;
            var name = dest.Name;
            var nextReady = dest.NextReady;
            var busy = dest.Portal;

            var box = new BoxContainer()
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(5f, 5f)
            };

            var nameLabel = new RichTextLabel();
            nameLabel.SetMessage(name);
            box.AddChild(nameLabel);

            var readyLabel = new Label
            {
                Text = ReadyText(now, nextReady, busy),
                Margin = new Thickness(10f, 0f, 0f, 0f)
            };
            _readyLabels.Add(readyLabel);
            box.AddChild(readyLabel);

            var openButton = new Button()
            {
                Text = Loc.GetString("gateway-window-open-portal"),
                Pressed = ent == _current,
                ToggleMode = true,
                Disabled = _current != null || busy || now < nextReady
            };

            openButton.OnPressed += args =>
            {
                OpenPortal?.Invoke(ent);
            };

            if (ent == _current)
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
            NextReadyBar.Value = 0f;
            NextCloseText.Text = "00:00";
        }
        else
        {
            var remaining = _nextReady - _timing.CurTime;
            if (remaining < TimeSpan.Zero)
            {
                NextReadyBar.Value = 1f;
                NextCloseText.Text = "00:00";
            }
            else
            {
                var openTime = _nextReady - _lastOpen;
                NextReadyBar.Value = 1f - (float) (remaining / openTime);
                NextCloseText.Text = $"{remaining.Minutes:00}:{remaining.Seconds:00}";
            }
        }

        for (var i = 0; i < _destinations.Count; i++)
        {
            var dest = _destinations[i];
            var nextReady = dest.NextReady;
            var busy = dest.Portal;
            _readyLabels[i].Text = ReadyText(now, nextReady, busy);
            _openButtons[i].Disabled = _current != null || busy || now < nextReady;
        }
    }

    private string ReadyText(TimeSpan now, TimeSpan nextReady, bool busy)
    {
        if (busy)
            return Loc.GetString("gateway-window-already-active");

        if (now < nextReady)
        {
            var time = nextReady - now;
            return Loc.GetString("gateway-window-ready-in", ("time", $"{time.Minutes:00}:{time.Seconds:00}"));
        }

        return Loc.GetString("gateway-window-ready");
    }
}
