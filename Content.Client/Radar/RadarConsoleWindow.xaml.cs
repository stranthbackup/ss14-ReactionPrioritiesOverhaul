using System;
using Content.Client.Computer;
using Content.Shared.Radar;
using JetBrains.Annotations;
using Robust.Client.AutoGenerated;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;

namespace Content.Client.Radar;

[GenerateTypedNameReferences]
public partial class RadarConsoleWindow : DefaultWindow, IComputerWindow<RadarConsoleBoundInterfaceState>
{
    public RadarConsoleWindow()
    {
        RobustXamlLoader.Load(this);
    }

    public void SetupComputerWindow(ComputerBoundUserInterfaceBase cb)
    {

    }

    public void UpdateState(RadarConsoleBoundInterfaceState scc)
    {
        Radar.UpdateState(scc);
    }
}


public sealed class RadarControl : Control
{
    private const int RadarArea = 512;

    private const int RadarCircleRadius = (RadarArea - 8) / 2;

    private RadarConsoleBoundInterfaceState _lastState = new RadarConsoleBoundInterfaceState(new RadarObjectData[] { }, Vector2i.Zero);

    private int SizeFull => (int) (RadarArea * UIScale);

    public int RadiusCircle => (int) (RadarCircleRadius * UIScale);

    public RadarControl()
    {
        MinSize = (SizeFull, SizeFull);
    }

    public void UpdateState(RadarConsoleBoundInterfaceState ls)
    {
        _lastState = ls;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var point = SizeFull / 2;
        var fakeAA = new Color(0.08f, 0.08f, 0.08f);
        var gridLines = new Color(0.08f, 0.08f, 0.08f);
        var gridLinesRadial = 8;
        var gridLinesEquatorial = 8;

        handle.DrawCircle((point, point), RadiusCircle + 1, fakeAA);
        handle.DrawCircle((point, point), RadiusCircle, Color.Black);

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

        handle.DrawLine((point, point) + new Vector2(8, 8), (point, point) - new Vector2(0, 8), Color.Yellow);
        handle.DrawLine((point, point) + new Vector2(-8, 8), (point, point) - new Vector2(0, 8), Color.Yellow);

        foreach (var obj in _lastState.Objects)
        {
            if (obj.Position.Length > RadiusCircle - 24)
                continue;

            switch (obj.Shape)
            {
                case ObjectShape.CircleFilled:
                case ObjectShape.Circle:
                {
                    handle.DrawCircle(obj.Position + point, obj.Radius, obj.Color, obj.Shape == ObjectShape.CircleFilled);
                    break;
                }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}

[UsedImplicitly]
public class RadarConsoleBoundUserInterface : ComputerBoundUserInterface<RadarConsoleWindow, RadarConsoleBoundInterfaceState>
{
    public RadarConsoleBoundUserInterface(ClientUserInterfaceComponent owner, object uiKey) : base(owner, uiKey) {}
}
