using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Radiation.Components;

namespace Content.Server.Radiation.Systems;

// create and update map of radiation blockers
public partial class RadiationSystem
{
    // dict of grid uid -> grid pos -> resistance value
    // no float value - no resistance on this tile
    // if grid has no resistance tile - it should be deleted
    private readonly Dictionary<EntityUid, Dictionary<Vector2i, float>> _resistancePerTile = new();

    private void InitRadBlocking()
    {
        SubscribeLocalEvent<RadiationBlockerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RadiationBlockerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RadiationBlockerComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<RadiationBlockerComponent, ReAnchorEvent>(OnReAnchor);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);

        SubscribeLocalEvent<RadiationBlockerComponent, DoorStateChangedEvent>(OnDoorChanged);
    }

    private void OnInit(EntityUid uid, RadiationBlockerComponent component, ComponentInit args)
    {
        if (!component.Enabled)
            return;
        AddTile(uid, component);
    }

    private void OnShutdown(EntityUid uid, RadiationBlockerComponent component, ComponentShutdown args)
    {
        if (component.Enabled)
            return;
        RemoveTile(uid, component);
    }

    private void OnAnchorChanged(EntityUid uid, RadiationBlockerComponent component, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
        {
            AddTile(uid, component);
        }
        else
        {
            RemoveTile(uid, component);
        }
    }

    private void OnReAnchor(EntityUid uid, RadiationBlockerComponent component, ref ReAnchorEvent args)
    {
        // probably grid was split
        // we need to remove entity from old resistance map
        RemoveTile(uid, component);
        // and move it to the new one
        AddTile(uid, component);
    }

    private void OnGridRemoved(GridRemovalEvent ev)
    {
        _resistancePerTile.Remove(ev.EntityUid);
    }

    private void OnDoorChanged(EntityUid uid, RadiationBlockerComponent component, DoorStateChangedEvent args)
    {
        if (args.State == DoorState.Open)
            SetEnabled(uid, false, component);
        else if (args.State == DoorState.Closed)
            SetEnabled(uid, true, component);
    }

    public void SetEnabled(EntityUid uid, bool isEnabled, RadiationBlockerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;
        if (isEnabled == component.Enabled)
            return;
        component.Enabled = isEnabled;

        if (!component.Enabled)
            RemoveTile(uid, component);
        else
            AddTile(uid, component);
    }

    private void AddTile(EntityUid uid, RadiationBlockerComponent component)
    {
        // check that last position was removed
        if (component.LastPosition != null)
        {
            RemoveTile(uid, component);
        }

        // check if entity even provide some rad protection
        if (!component.Enabled || component.RadResistance <= 0)
            return;

        // check if it's on a grid
        var trs = Transform(uid);
        if (!trs.Anchored || trs.GridUid == null || !TryComp(trs.GridUid, out IMapGridComponent? grid))
            return;

        // save resistance into rad protection grid
        var gridId = trs.GridUid.Value;
        var tilePos = grid.Grid.TileIndicesFor(trs.Coordinates);
        AddToTile(gridId, tilePos, component.RadResistance);

        // and remember it as last valid position
        component.LastPosition = (gridId, tilePos);
    }

    private void RemoveTile(EntityUid uid, RadiationBlockerComponent component)
    {
        // check if blocker was placed on grid before component was removed
        if (component.LastPosition == null)
            return;
        var (gridId, tilePos) = component.LastPosition.Value;

        // try to remove
        RemoveFromTile(gridId, tilePos, component.RadResistance);
        component.LastPosition = null;
    }

    private void AddToTile(EntityUid gridId, Vector2i tilePos, float radResistance)
    {
        // get existing rad resistance grid or create it if it doesn't exist
        if (!_resistancePerTile.ContainsKey(gridId))
        {
            _resistancePerTile.Add(gridId, new Dictionary<Vector2i, float>());
        }
        var grid = _resistancePerTile[gridId];

        // add to existing cell more rad resistance
        var newResistance = radResistance;
        if (grid.TryGetValue(tilePos, out var existingResistance))
        {
            newResistance += existingResistance;
        }
        grid[tilePos] = newResistance;
    }

    private void RemoveFromTile(EntityUid gridId, Vector2i tilePos, float radResistance)
    {
        // get grid
        if (!_resistancePerTile.ContainsKey(gridId))
            return;
        var grid = _resistancePerTile[gridId];

        // subtract resistance from tile
        if (!grid.TryGetValue(tilePos, out var existingResistance))
            return;
        existingResistance -= radResistance;

        // remove tile from grid if no resistance left
        if (existingResistance > 0)
            grid[tilePos] = existingResistance;
        else
        {
            grid.Remove(tilePos);
            if (grid.Count == 0)
                _resistancePerTile.Remove(gridId);
        }
    }
}
