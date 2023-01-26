using Content.Shared.Wires;
using Robust.Client.GameObjects;

namespace Content.Client.Power.Visualizers;

public sealed class CableVisualizerSystem : VisualizerSystem<CableVisualizerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, CableVisualizerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!args.Sprite.Visible)
        {
            // This entity is probably below a floor and is not even visible to the user -> don't bother updating sprite data.
            // Note that if the subfloor visuals change, then another AppearanceChangeEvent will get triggered.
            return;
        }

        if (!AppearanceSystem.TryGetData(uid, WireVisVisuals.ConnectedMask, out WireVisDirFlags mask, args.Component))
            mask = WireVisDirFlags.None;

        args.Sprite.LayerSetState(0, $"{component.StatePrefix}{(int) mask}");
    }

}
