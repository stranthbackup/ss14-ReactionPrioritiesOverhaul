using Content.Client.SubFloor;
using Content.Shared.SubFloor;
using Content.Shared.Wires;
using Robust.Client.GameObjects;

namespace Content.Client.Power.Visualizers;

public sealed class CableVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CableVisualizerComponent, AppearanceChangeEvent>(OnAppearanceChanged, after: new[] { typeof(SubFloorHideSystem) });
    }

    private void OnAppearanceChanged(EntityUid uid, CableVisualizerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.Sprite.Visible)
        {
            // This entity is probably below a floor and is not even visible to the user -> don't bother updating sprite data.
            // Note that if the subfloor visuals change, then another AppearanceChangeEvent will get triggered.
            return;
        }

        if (!_appearance.TryGetData(uid, WireVisVisuals.ConnectedMask, out WireVisDirFlags mask, args.Component))
            mask = WireVisDirFlags.None;

        args.Sprite.LayerSetState(0, $"{component.StatePrefix}{(int) mask}");
    }

}
