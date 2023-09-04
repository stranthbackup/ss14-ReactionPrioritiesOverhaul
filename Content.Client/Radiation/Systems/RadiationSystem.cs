using Content.Client.Radiation.Overlays;
using Content.Shared.Radiation.Events;
using Content.Shared.Radiation.Systems;
using Robust.Client.Graphics;

namespace Content.Client.Radiation.Systems;

[InjectDependencies]
public sealed partial class RadiationSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayMan = default!;

    public List<RadiationRay>? Rays;
    public Dictionary<EntityUid, Dictionary<Vector2i, float>>? ResistanceGrids;

    public override void Initialize()
    {
        SubscribeNetworkEvent<OnRadiationOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<OnRadiationOverlayUpdateEvent>(OnOverlayUpdate);
        SubscribeNetworkEvent<OnRadiationOverlayResistanceUpdateEvent>(OnResistanceUpdate);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<RadiationDebugOverlay>();
    }

    private void OnOverlayToggled(OnRadiationOverlayToggledEvent ev)
    {
        if (ev.IsEnabled)
            _overlayMan.AddOverlay(new RadiationDebugOverlay());
        else
            _overlayMan.RemoveOverlay<RadiationDebugOverlay>();
    }

    private void OnOverlayUpdate(OnRadiationOverlayUpdateEvent ev)
    {
        if (!_overlayMan.TryGetOverlay(out RadiationDebugOverlay? overlay))
            return;

        var str = $"Radiation update: {ev.ElapsedTimeMs}ms with. Receivers: {ev.ReceiversCount}, " +
                  $"Sources: {ev.SourcesCount}, Rays: {ev.Rays.Count}";
        Logger.Info(str);

        Rays = ev.Rays;
    }

    private void OnResistanceUpdate(OnRadiationOverlayResistanceUpdateEvent ev)
    {
        if (!_overlayMan.TryGetOverlay(out RadiationDebugOverlay? overlay))
            return;
        ResistanceGrids = ev.Grids;
    }
}
