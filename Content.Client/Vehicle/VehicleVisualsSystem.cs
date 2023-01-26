using Robust.Client.GameObjects;
using Content.Shared.Vehicle;

namespace Content.Client.Vehicle
{
    /// <summary>
    /// Controls client-side visuals for
    /// vehicles
    /// </summary>
    public sealed class VehicleVisualsSystem : VisualizerSystem<VehicleVisualsComponent>
    {
        [Dependency] private readonly AppearanceSystem _appearance = default!;

        protected override void OnAppearanceChange(EntityUid uid, VehicleVisualsComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;

            // First check is for the sprite itself
            if (_appearance.TryGetData(uid, VehicleVisuals.DrawDepth, out int drawDepth, args.Component))
            {
                args.Sprite.DrawDepth = drawDepth;
            }

            // Set vehicle layer to animated or not (i.e. are the wheels turning or not)
            if (_appearance.TryGetData(uid, VehicleVisuals.AutoAnimate, out bool autoAnimate, args.Component))
            {
                args.Sprite.LayerSetAutoAnimated(VehicleVisualLayers.AutoAnimate, autoAnimate);
            }
        }
    }
}
public enum VehicleVisualLayers : byte
{
    /// Layer for the vehicle's wheels
    AutoAnimate,
}
