using Content.Server.Access.Systems;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Medical.CrewMonitoring;
using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory.Events;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Medical.SuitSensors
{
    public sealed class SuitSensorSystem : EntitySystem
    {
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IdCardSystem _idCardSystem = default!;
        [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
        [Dependency] private readonly DeviceNetworkSystem _deviceNetworkSystem = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly CrewMonitoringServerSystem _monitoringServerSystem = default!;
        [Dependency] private readonly StationSystem _stationSystem = default!;
        [Dependency] private readonly SharedTransformSystem _xform = default!;

        private const float UpdateRate = 1f;
        private float _updateDif;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SuitSensorComponent, MapInitEvent>(OnMapInit);
            SubscribeLocalEvent<SuitSensorComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<SuitSensorComponent, GotEquippedEvent>(OnEquipped);
            SubscribeLocalEvent<SuitSensorComponent, GotUnequippedEvent>(OnUnequipped);
            SubscribeLocalEvent<SuitSensorComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<SuitSensorComponent, GetVerbsEvent<Verb>>(OnVerb);
            SubscribeLocalEvent<SuitSensorComponent, EntGotInsertedIntoContainerMessage>(OnInsert);
            SubscribeLocalEvent<SuitSensorComponent, EntGotRemovedFromContainerMessage>(OnRemove);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // check update rate
            _updateDif += frameTime;
            if (_updateDif < UpdateRate)
                return;

            _updateDif -= UpdateRate;

            var curTime = _gameTiming.CurTime;
            var sensors = EntityManager.EntityQuery<SuitSensorComponent, DeviceNetworkComponent>();
            foreach (var (sensor, device) in sensors)
            {
                if (!device.TransmitFrequency.HasValue || !sensor.StationId.HasValue)
                    continue;

                // check if sensor is ready to update
                if (curTime - sensor.LastUpdate < sensor.UpdateRate)
                    continue;

                // Add a random offset to the next update time that isn't longer than the sensors update rate
                sensor.LastUpdate = curTime.Add(TimeSpan.FromSeconds(_random.Next(0, sensor.UpdateRate.Seconds)));

                // get sensor status
                var status = GetSensorState(sensor.Owner, sensor);
                if (status == null)
                    continue;

                //Retrieve active server address if the sensor isn't connected to a server
                if (sensor.ConnectedServer == null)
                {
                    if (!_monitoringServerSystem.TryGetActiveServerAddress(sensor.StationId.Value, out var address))
                        continue;

                    sensor.ConnectedServer = address;
                }

                // Send it to the connected server
                var payload = SuitSensorToPacket(status);

                // Clear the connected server if its address isn't on the network
                if (!_deviceNetworkSystem.IsAddressPresent(device.DeviceNetId, sensor.ConnectedServer))
                {
                    sensor.ConnectedServer = null;
                    continue;
                }

                _deviceNetworkSystem.QueuePacket(sensor.Owner, sensor.ConnectedServer, payload, device: device);
            }
        }

        private void OnMapInit(EntityUid uid, SuitSensorComponent component, MapInitEvent args)
        {
            // generate random mode
            if (component.RandomMode)
            {
                //make the sensor mode favor higher levels, except coords.
                var modesDist = new[]
                {
                    SuitSensorMode.SensorOff,
                    SuitSensorMode.SensorBinary, SuitSensorMode.SensorBinary,
                    SuitSensorMode.SensorVitals, SuitSensorMode.SensorVitals, SuitSensorMode.SensorVitals,
                    SuitSensorMode.SensorCords, SuitSensorMode.SensorCords
                };
                component.Mode = _random.Pick(modesDist);
            }
        }

        private void OnComponentInit(EntityUid uid, SuitSensorComponent component, ComponentInit args)
        {
            component.StationId = _stationSystem.GetOwningStation(uid);
        }

        private void OnEquipped(EntityUid uid, SuitSensorComponent component, GotEquippedEvent args)
        {
            if (args.Slot != component.ActivationSlot)
                return;

            component.User = args.Equipee;
        }

        private void OnUnequipped(EntityUid uid, SuitSensorComponent component, GotUnequippedEvent args)
        {
            if (args.Slot != component.ActivationSlot)
                return;

            component.User = null;
        }

        private void OnExamine(EntityUid uid, SuitSensorComponent component, ExaminedEvent args)
        {
            if (!args.IsInDetailsRange)
                return;

            string msg;
            switch (component.Mode)
            {
                case SuitSensorMode.SensorOff:
                    msg = "suit-sensor-examine-off";
                    break;
                case SuitSensorMode.SensorBinary:
                    msg = "suit-sensor-examine-binary";
                    break;
                case SuitSensorMode.SensorVitals:
                    msg = "suit-sensor-examine-vitals";
                    break;
                case SuitSensorMode.SensorCords:
                    msg = "suit-sensor-examine-cords";
                    break;
                default:
                    return;
            }

            args.PushMarkup(Loc.GetString(msg));
        }

        private void OnVerb(EntityUid uid, SuitSensorComponent component, GetVerbsEvent<Verb> args)
        {
            // check if user can change sensor
            if (component.ControlsLocked)
                return;

            // standard interaction checks
            if (!args.CanAccess || !args.CanInteract || args.Hands == null)
                return;

            args.Verbs.UnionWith(new[]
            {
                CreateVerb(uid, component, args.User, SuitSensorMode.SensorOff),
                CreateVerb(uid, component, args.User, SuitSensorMode.SensorBinary),
                CreateVerb(uid, component, args.User, SuitSensorMode.SensorVitals),
                CreateVerb(uid, component, args.User, SuitSensorMode.SensorCords)
            });
        }

        private void OnInsert(EntityUid uid, SuitSensorComponent component, EntGotInsertedIntoContainerMessage args)
        {
            if (args.Container.ID != component.ActivationContainer)
                return;

            component.User = args.Container.Owner;
        }

        private void OnRemove(EntityUid uid, SuitSensorComponent component, EntGotRemovedFromContainerMessage args)
        {
            if (args.Container.ID != component.ActivationContainer)
                return;

            component.User = null;
        }

        private Verb CreateVerb(EntityUid uid, SuitSensorComponent component, EntityUid userUid, SuitSensorMode mode)
        {
            return new Verb()
            {
                Text = GetModeName(mode),
                Disabled = component.Mode == mode,
                Priority = -(int) mode, // sort them in descending order
                Category = VerbCategory.SetSensor,
                Act = () => SetSensor(uid, mode, userUid, component)
            };
        }

        private string GetModeName(SuitSensorMode mode)
        {
            string name;
            switch (mode)
            {
                case SuitSensorMode.SensorOff:
                    name = "suit-sensor-mode-off";
                    break;
                case SuitSensorMode.SensorBinary:
                    name = "suit-sensor-mode-binary";
                    break;
                case SuitSensorMode.SensorVitals:
                    name = "suit-sensor-mode-vitals";
                    break;
                case SuitSensorMode.SensorCords:
                    name = "suit-sensor-mode-cords";
                    break;
                default:
                    return "";
            }

            return Loc.GetString(name);
        }

        public void SetSensor(EntityUid uid, SuitSensorMode mode, EntityUid? userUid = null,
            SuitSensorComponent? component = null)
        {
            if (!Resolve(uid, ref component))
                return;

            component.Mode = mode;

            if (userUid != null)
            {
                var msg = Loc.GetString("suit-sensor-mode-state", ("mode", GetModeName(mode)));
                _popupSystem.PopupEntity(msg, uid, userUid.Value);
            }
        }

        public SuitSensorStatus? GetSensorState(EntityUid uid, SuitSensorComponent? sensor = null, TransformComponent? transform = null)
        {
            if (!Resolve(uid, ref sensor, ref transform))
                return null;

            // check if sensor is enabled and worn by user
            if (sensor.Mode == SuitSensorMode.SensorOff || sensor.User == null || transform.GridUid == null)
                return null;

            // try to get mobs id from ID slot
            var userName = Loc.GetString("suit-sensor-component-unknown-name");
            var userJob = Loc.GetString("suit-sensor-component-unknown-job");
            if (_idCardSystem.TryFindIdCard(sensor.User.Value, out var card))
            {
                if (card.FullName != null)
                    userName = card.FullName;
                if (card.JobTitle != null)
                    userJob = card.JobTitle;
            }

            // get health mob state
            var isAlive = false;
            if (EntityManager.TryGetComponent(sensor.User.Value, out MobStateComponent? mobState))
                isAlive = _mobStateSystem.IsAlive(sensor.User.Value, mobState);

            // get mob total damage
            var totalDamage = 0;
            if (TryComp<DamageableComponent>(sensor.User.Value, out var damageable))
                totalDamage = damageable.TotalDamage.Int();

            // finally, form suit sensor status
            var xForm = Transform(sensor.User.Value);
            var xFormQuery = GetEntityQuery<TransformComponent>();
            var coords = _xform.GetMoverCoordinates(xForm, xFormQuery);
            var status = new SuitSensorStatus(userName, userJob);
            switch (sensor.Mode)
            {
                case SuitSensorMode.SensorBinary:
                    status.IsAlive = isAlive;
                    break;
                case SuitSensorMode.SensorVitals:
                    status.IsAlive = isAlive;
                    status.TotalDamage = totalDamage;
                    break;
                case SuitSensorMode.SensorCords:
                    status.IsAlive = isAlive;
                    status.TotalDamage = totalDamage;
                    status.Coordinates = coords;
                    break;
            }

            return status;
        }

        /// <summary>
        ///     Serialize create a device network package from the suit sensors status.
        /// </summary>
        public NetworkPayload SuitSensorToPacket(SuitSensorStatus status)
        {
            var payload = new NetworkPayload()
            {
                [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
                [SuitSensorConstants.NET_NAME] = status.Name,
                [SuitSensorConstants.NET_JOB] = status.Job,
                [SuitSensorConstants.NET_IS_ALIVE] = status.IsAlive,
            };

            if (status.TotalDamage != null)
                payload.Add(SuitSensorConstants.NET_TOTAL_DAMAGE, status.TotalDamage);
            if (status.Coordinates != null)
                payload.Add(SuitSensorConstants.NET_CORDINATES, status.Coordinates);


            return payload;
        }

        /// <summary>
        ///     Try to create the suit sensors status from the device network message
        /// </summary>
        public SuitSensorStatus? PacketToSuitSensor(NetworkPayload payload)
        {
            // check command
            if (!payload.TryGetValue(DeviceNetworkConstants.Command, out string? command))
                return null;
            if (command != DeviceNetworkConstants.CmdUpdatedState)
                return null;

            // check name, job and alive
            if (!payload.TryGetValue(SuitSensorConstants.NET_NAME, out string? name)) return null;
            if (!payload.TryGetValue(SuitSensorConstants.NET_JOB, out string? job)) return null;
            if (!payload.TryGetValue(SuitSensorConstants.NET_IS_ALIVE, out bool? isAlive)) return null;

            // try get total damage and cords (optionals)
            payload.TryGetValue(SuitSensorConstants.NET_TOTAL_DAMAGE, out int? totalDamage);
            payload.TryGetValue(SuitSensorConstants.NET_CORDINATES, out EntityCoordinates? cords);

            var status = new SuitSensorStatus(name, job)
            {
                IsAlive = isAlive.Value,
                TotalDamage = totalDamage,
                Coordinates = cords,
            };
            return status;
        }
    }
}
