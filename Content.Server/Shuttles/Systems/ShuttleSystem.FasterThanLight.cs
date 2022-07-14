using System.Diagnostics.CodeAnalysis;
using Content.Server.Buckle.Components;
using Content.Server.Doors.Components;
using Content.Server.Doors.Systems;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Server.Stunnable;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Sound;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.Collections;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /*
     * This is a way to move a shuttle from one location to another, via an intermediate map for fanciness.
     */

    [Dependency] private readonly DoorSystem _doors = default!;
    [Dependency] private readonly ShuttleConsoleSystem _console = default!;
    [Dependency] private readonly StunSystem _stuns = default!;

    private MapId? _hyperSpaceMap;

    private const float DefaultStartupTime = 5.5f;
    private const float DefaultTravelTime = 30f;
    private const float FTLCooldown = 30f;

    private const float ShuttleFTLRange = 100f;

    /// <summary>
    /// Minimum mass a grid needs to be to block a shuttle recall.
    /// </summary>
    private const float ShuttleFTLMassThreshold = 300f;

    // I'm too lazy to make CVars.

    private readonly SoundSpecifier _startupSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_begin.ogg");
    // private SoundSpecifier _travelSound = new SoundPathSpecifier();
    private readonly SoundSpecifier _arrivalSound = new SoundPathSpecifier("/Audio/Effects/Shuttle/hyperspace_end.ogg");

    private readonly TimeSpan _hyperspaceKnockdownTime = TimeSpan.FromSeconds(5);

    /// Left-side of the station we're allowed to use
    private float _index;

    /// <summary>
    /// Space between grids within hyperspace.
    /// </summary>
    private const float Buffer = 5f;

    private void InitializeFTL()
    {
        SubscribeLocalEvent<StationGridAddedEvent>(OnStationGridAdd);
        SubscribeLocalEvent<FTLDestinationComponent, EntityPausedEvent>(OnDestinationPause);
    }

    private void OnDestinationPause(EntityUid uid, FTLDestinationComponent component, EntityPausedEvent args)
    {
        _console.RefreshShuttleConsoles();
    }

    private void OnStationGridAdd(StationGridAddedEvent ev)
    {
        if (TryComp<PhysicsComponent>(ev.GridId, out var body) && body.Mass > 500f)
        {
            AddFTLDestination(ev.GridId, true);
        }
    }

    public bool CanFTL(EntityUid? uid, [NotNullWhen(false)] out string? reason, TransformComponent? xform = null)
    {
        reason = null;

        if (!TryComp<IMapGridComponent>(uid, out var grid) ||
            !Resolve(uid.Value, ref xform)) return true;

        var bounds = grid.Grid.WorldAABB.Enlarged(ShuttleFTLRange);
        var bodyQuery = GetEntityQuery<PhysicsComponent>();

        foreach (var other in _mapManager.FindGridsIntersecting(xform.MapID, bounds))
        {
            if (grid.GridIndex == other.Index ||
                !bodyQuery.TryGetComponent(other.GridEntityId, out var body) ||
                body.Mass < ShuttleFTLMassThreshold) continue;

            reason = Loc.GetString("shuttle-console-proximity");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds a target for hyperspace to every shuttle console.
    /// </summary>
    public FTLDestinationComponent AddFTLDestination(EntityUid uid, bool enabled)
    {
        if (TryComp<FTLDestinationComponent>(uid, out var destination) && destination.Enabled == enabled) return destination;

        destination = EnsureComp<FTLDestinationComponent>(uid);

        if (HasComp<FTLComponent>(uid))
        {
            enabled = false;
        }

        destination.Enabled = enabled;
        _console.RefreshShuttleConsoles();
        return destination;
    }

    public void RemoveFTLDestination(EntityUid uid)
    {
        if (!RemComp<FTLDestinationComponent>(uid)) return;
        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Moves a shuttle from its current position to the target one. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLTravel(ShuttleComponent component,
        EntityCoordinates coordinates,
        float startupTime = DefaultStartupTime,
        float hyperspaceTime = DefaultTravelTime)
    {
        if (!TrySetupFTL(component, out var hyperspace))
           return;

        hyperspace.StartupTime = startupTime;
        hyperspace.TravelTime = hyperspaceTime;
        hyperspace.Accumulator = hyperspace.StartupTime;
        hyperspace.TargetCoordinates = coordinates;
        hyperspace.Dock = false;
        _console.RefreshShuttleConsoles();
    }

    /// <summary>
    /// Moves a shuttle from its current position to docked on the target one. Goes through the hyperspace map while the timer is running.
    /// </summary>
    public void FTLTravel(ShuttleComponent component,
        EntityUid target,
        float startupTime = DefaultStartupTime,
        float hyperspaceTime = DefaultTravelTime,
        bool dock = false)
    {
        if (!TrySetupFTL(component, out var hyperspace))
            return;

        hyperspace.State = FTLState.Starting;
        hyperspace.StartupTime = startupTime;
        hyperspace.TravelTime = hyperspaceTime;
        hyperspace.Accumulator = hyperspace.StartupTime;
        hyperspace.TargetUid = target;
        hyperspace.Dock = dock;
        _console.RefreshShuttleConsoles();
    }

    private bool TrySetupFTL(ShuttleComponent shuttle, [NotNullWhen(true)] out FTLComponent? component)
    {
        var uid = shuttle.Owner;
        component = null;

        if (HasComp<FTLComponent>(uid))
        {
            _sawmill.Warning($"Tried queuing {ToPrettyString(uid)} which already has HyperspaceComponent?");
            return false;
        }

        if (TryComp<FTLDestinationComponent>(uid, out var dest))
        {
            dest.Enabled = false;
        }

        // TODO: Maybe move this to docking instead?
        SetDocks(uid, false);

        component = AddComp<FTLComponent>(uid);
        // TODO: Need BroadcastGrid to not be bad.
        SoundSystem.Play(_startupSound.GetSound(), Filter.Pvs(component.Owner, GetSoundRange(component.Owner), entityManager: EntityManager), _startupSound.Params);
        return true;
    }

    private void UpdateHyperspace(float frameTime)
    {
        foreach (var comp in EntityQuery<FTLComponent>())
        {
            comp.Accumulator -= frameTime;

            if (comp.Accumulator > 0f) continue;

            var xform = Transform(comp.Owner);
            PhysicsComponent? body;

            switch (comp.State)
            {
                // Startup time has elapsed and in hyperspace.
                case FTLState.Starting:
                    DoTheDinosaur(xform);

                    comp.State = FTLState.Travelling;
                    SetupHyperspace();

                    var width = Comp<IMapGridComponent>(comp.Owner).Grid.LocalAABB.Width;
                    xform.Coordinates = new EntityCoordinates(_mapManager.GetMapEntityId(_hyperSpaceMap!.Value), new Vector2(_index + width / 2f, 0f));
                    xform.LocalRotation = Angle.Zero;
                    _index += width + Buffer;
                    comp.Accumulator += comp.TravelTime;

                    if (TryComp(comp.Owner, out body))
                    {
                        body.LinearVelocity = new Vector2(0f, 20f);
                        body.AngularVelocity = 0f;
                        body.LinearDamping = 0f;
                        body.AngularDamping = 0f;
                    }

                    if (comp.TravelSound != null)
                    {
                        comp.TravelStream = SoundSystem.Play(comp.TravelSound.GetSound(),
                            Filter.Pvs(comp.Owner, 4f, entityManager: EntityManager), comp.TravelSound.Params);
                    }

                    SetDockBolts(comp.Owner, true);
                    _console.RefreshShuttleConsoles(comp.Owner);
                    break;
                // Arrive.
                case FTLState.Travelling:
                    DoTheDinosaur(xform);
                    SetDockBolts(comp.Owner, false);
                    SetDocks(comp.Owner, true);

                    if (TryComp(comp.Owner, out body))
                    {
                        body.LinearVelocity = Vector2.Zero;
                        body.AngularVelocity = 0f;
                        body.LinearDamping = ShuttleIdleLinearDamping;
                        body.AngularDamping = ShuttleIdleAngularDamping;
                    }

                    TryComp<ShuttleComponent>(comp.Owner, out var shuttle);

                    if (comp.TargetUid != null && shuttle != null)
                    {
                        if (comp.Dock)
                            TryHyperspaceDock(shuttle, comp.TargetUid.Value);
                        else
                            TryHyperspaceProximity(shuttle, comp.TargetUid.Value);
                    }
                    else
                    {
                        xform.Coordinates = comp.TargetCoordinates;
                    }

                    if (comp.TravelStream != null)
                    {
                        comp.TravelStream?.Stop();
                        comp.TravelStream = null;
                    }

                    SoundSystem.Play(_arrivalSound.GetSound(),
                        Filter.Pvs(comp.Owner, GetSoundRange(comp.Owner), entityManager: EntityManager));

                    if (TryComp<FTLDestinationComponent>(comp.Owner, out var dest))
                    {
                        dest.Enabled = true;
                    }

                    comp.State = FTLState.Cooldown;
                    comp.Accumulator += FTLCooldown;
                    _console.RefreshShuttleConsoles(comp.Owner);
                    break;
                case FTLState.Cooldown:
                    RemComp<FTLComponent>(comp.Owner);
                    _console.RefreshShuttleConsoles(comp.Owner);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void SetDocks(EntityUid uid, bool enabled)
    {
        foreach (var (dock, xform) in EntityQuery<DockingComponent, TransformComponent>(true))
        {
            if (xform.ParentUid != uid || dock.Enabled == enabled) continue;
            _dockSystem.Undock(dock);
            dock.Enabled = enabled;
        }
    }

    private void SetDockBolts(EntityUid uid, bool enabled)
    {
        foreach (var (_, door, xform) in EntityQuery<DockingComponent, AirlockComponent, TransformComponent>(true))
        {
            if (xform.ParentUid != uid) continue;

            _doors.TryClose(door.Owner);
            door.SetBoltsWithAudio(enabled);
        }
    }

    private float GetSoundRange(EntityUid uid)
    {
        if (!_mapManager.TryGetGrid(uid, out var grid)) return 4f;

        return MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) + 12.5f;
    }

    private void SetupHyperspace()
    {
        if (_hyperSpaceMap != null) return;

        _hyperSpaceMap = _mapManager.CreateMap();
        _sawmill.Info($"Setup hyperspace map at {_hyperSpaceMap.Value}");
        DebugTools.Assert(!_mapManager.IsMapPaused(_hyperSpaceMap.Value));
    }

    private void CleanupHyperspace()
    {
        _index = 0f;
        if (_hyperSpaceMap == null || !_mapManager.MapExists(_hyperSpaceMap.Value))
        {
            _hyperSpaceMap = null;
            return;
        }
        _mapManager.DeleteMap(_hyperSpaceMap.Value);
        _hyperSpaceMap = null;
    }

    /// <summary>
    /// Puts everyone unbuckled on the floor, paralyzed.
    /// </summary>
    private void DoTheDinosaur(TransformComponent xform)
    {
        var buckleQuery = GetEntityQuery<BuckleComponent>();
        var statusQuery = GetEntityQuery<StatusEffectsComponent>();
        // Get enumeration exceptions from people dropping things if we just paralyze as we go
        var toKnock = new ValueList<EntityUid>();

        KnockOverKids(xform, buckleQuery, statusQuery, ref toKnock);

        foreach (var child in toKnock)
        {
            if (!statusQuery.TryGetComponent(child, out var status)) continue;
            _stuns.TryParalyze(child, _hyperspaceKnockdownTime, true, status);
        }
    }

    private void KnockOverKids(TransformComponent xform, EntityQuery<BuckleComponent> buckleQuery, EntityQuery<StatusEffectsComponent> statusQuery, ref ValueList<EntityUid> toKnock)
    {
        // Not recursive because probably not necessary? If we need it to be that's why this method is separate.
        var childEnumerator = xform.ChildEnumerator;

        while (childEnumerator.MoveNext(out var child))
        {
            if (!buckleQuery.TryGetComponent(child.Value, out var buckle) || buckle.Buckled) continue;

            toKnock.Add(child.Value);
        }
    }

    /// <summary>
    /// Tries to dock with the target grid, otherwise falls back to proximity.
    /// </summary>
    private bool TryHyperspaceDock(ShuttleComponent component, EntityUid targetUid)
    {
        if (!TryComp<TransformComponent>(component.Owner, out var xform) ||
            !TryComp<TransformComponent>(targetUid, out var targetXform) ||
            targetXform.MapUid == null) return false;

        var config = GetDockingConfig(component, targetUid);

        if (config != null)
        {
           // Set position
           xform.Coordinates = config.Coordinates;
           xform.WorldRotation = config.Angle;

           // Connect everything
           foreach (var (dockA, dockB) in config.Docks)
           {
               _dockSystem.Dock(dockA, dockB);
           }

           return true;
        }

        TryHyperspaceProximity(component, targetUid, xform, targetXform);
        return false;
    }

    /// <summary>
    /// Tries to arrive nearby without overlapping with other grids.
    /// </summary>
    private bool TryHyperspaceProximity(ShuttleComponent component, EntityUid targetUid, TransformComponent? xform = null, TransformComponent? targetXform = null)
    {
        if (!Resolve(targetUid, ref targetXform) || targetXform.MapUid == null || !Resolve(component.Owner, ref xform)) return false;

        var shuttleAABB = Comp<IMapGridComponent>(component.Owner).Grid.WorldAABB;
        Box2? aabb = null;

        // Spawn nearby.
        foreach (var grid in _mapManager.GetAllMapGrids(targetXform.MapID))
        {
            var gridAABB = grid.WorldAABB;
            aabb = aabb?.Union(gridAABB) ?? gridAABB;
        }

        aabb ??= new Box2();

        var minRadius = MathF.Max(aabb.Value.Width, aabb.Value.Height) + MathF.Max(shuttleAABB.Width, shuttleAABB.Height);
        var spawnPos = aabb.Value.Center + _random.NextVector2(minRadius, minRadius + 256f);

        if (TryComp<PhysicsComponent>(component.Owner, out var shuttleBody))
        {
            shuttleBody.LinearVelocity = Vector2.Zero;
            shuttleBody.AngularVelocity = 0f;
        }

        xform.Coordinates = new EntityCoordinates(targetXform.MapUid.Value, spawnPos);
        xform.WorldRotation = _random.NextAngle();
        return false;
    }
}
