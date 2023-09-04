using Content.Server.PowerCell;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;

namespace Content.Server.Pinpointer;

[InjectDependencies]
public sealed partial class StationMapSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private PowerCellSystem _cell = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationMapUserComponent, EntParentChangedMessage>(OnUserParentChanged);
        SubscribeLocalEvent<StationMapComponent, BoundUIOpenedEvent>(OnStationMapOpened);
        SubscribeLocalEvent<StationMapComponent, BoundUIClosedEvent>(OnStationMapClosed);
    }

    private void OnStationMapClosed(EntityUid uid, StationMapComponent component, BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, StationMapUiKey.Key) || args.Session.AttachedEntity == null)
            return;

        RemCompDeferred<StationMapUserComponent>(args.Session.AttachedEntity.Value);
    }

    private void OnUserParentChanged(EntityUid uid, StationMapUserComponent component, ref EntParentChangedMessage args)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            _ui.TryClose(component.Map, StationMapUiKey.Key, actor.PlayerSession);
        }
    }

    private void OnStationMapOpened(EntityUid uid, StationMapComponent component, BoundUIOpenedEvent args)
    {
        if (args.Session.AttachedEntity == null)
            return;

        if (!_cell.TryUseActivatableCharge(uid))
            return;

        var comp = EnsureComp<StationMapUserComponent>(args.Session.AttachedEntity.Value);
        comp.Map = uid;
    }
}
