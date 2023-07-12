using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.Stunnable;
using Content.Shared.Administration;
using Content.Shared.Instruments;
using Content.Shared.Instruments.UI;
using Content.Shared.Popups;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Midi;
using Robust.Shared.Collections;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Timing;

namespace Content.Server.Instruments;

[UsedImplicitly]
public sealed partial class InstrumentSystem : SharedInstrumentSystem
{
    private const float MaxInstrumentBandRange = 10f;
    private const float BandRequestDelay = 1.0f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConsoleHost _conHost = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly StunSystem _stuns = default!;
    [Dependency] private readonly UserInterfaceSystem _bui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private TimeSpan _bandRequestTimer = TimeSpan.Zero;
    private readonly Queue<InstrumentBandRequestBuiMessage> _bandRequestQueue = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializeCVars();

        SubscribeNetworkEvent<InstrumentMidiEventEvent>(OnMidiEventRx);
        SubscribeNetworkEvent<InstrumentStartMidiEvent>(OnMidiStart);
        SubscribeNetworkEvent<InstrumentStopMidiEvent>(OnMidiStop);
        SubscribeNetworkEvent<InstrumentSetMasterEvent>(OnMidiSetMaster);
        SubscribeNetworkEvent<InstrumentSetMasterChannelEvent>(OnMidiSetMasterChannel);

        SubscribeLocalEvent<InstrumentComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<InstrumentComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<InstrumentComponent, InstrumentBandRequestBuiMessage>(OnBoundUIRequestBands);

        _conHost.RegisterCommand("addtoband", AddToBandCommand);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void AddToBandCommand(IConsoleShell shell, string _, string[] args)
    {
        if (!EntityUid.TryParse(args[0], out var firstUid))
        {
            shell.WriteError($"Cannot parse first Uid");
            return;
        }

        if (!EntityUid.TryParse(args[1], out var secondUid))
        {
            shell.WriteError($"Cannot parse second Uid");
            return;
        }

        if (!int.TryParse(args[2], out var channel) || channel >= RobustMidiEvent.MaxChannels)
        {
            shell.WriteError($"Cannot parse MIDI Channel or higher than max ({RobustMidiEvent.MaxChannels})");
            return;
        }

        if (!HasComp<ActiveInstrumentComponent>(secondUid))
        {
            shell.WriteError($"Puppet instrument is not active!");
            return;
        }

        var otherInstrument = Comp<InstrumentComponent>(secondUid);
        otherInstrument.Playing = true;
        otherInstrument.Master = firstUid;
        otherInstrument.MasterChannels[channel] = true;
        Dirty(secondUid, otherInstrument);
    }

    private void OnMidiStart(InstrumentStartMidiEvent msg, EntitySessionEventArgs args)
    {
        var uid = msg.Uid;

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (args.SenderSession != instrument.InstrumentPlayer)
            return;

        instrument.Playing = true;
        Dirty(uid, instrument);
    }

    private void OnMidiStop(InstrumentStopMidiEvent msg, EntitySessionEventArgs args)
    {
        var uid = msg.Uid;

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (args.SenderSession != instrument.InstrumentPlayer)
            return;

        Clean(uid, instrument);
    }

    private void OnMidiSetMaster(InstrumentSetMasterEvent msg, EntitySessionEventArgs args)
    {
        var uid = msg.Uid;
        var master = msg.Master;

        if (!HasComp<ActiveInstrumentComponent>(uid))
            return;

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (args.SenderSession != instrument.InstrumentPlayer)
            return;

        if (master != null)
        {
            if (!HasComp<ActiveInstrumentComponent>(master))
                return;

            if (!TryComp<InstrumentComponent>(master, out var masterInstrument) || masterInstrument.Master != null)
                return;

            instrument.Master = master;
            instrument.MasterChannels.SetAll(true);
            instrument.Playing = true;
            Dirty(uid, instrument);
            return;
        }

        Clean(uid, instrument);
    }

    private void OnMidiSetMasterChannel(InstrumentSetMasterChannelEvent msg, EntitySessionEventArgs args)
    {
        var uid = msg.Uid;

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (instrument.Master == null)
            return;

        if (args.SenderSession != instrument.InstrumentPlayer)
            return;

        instrument.MasterChannels[msg.Channel] = msg.Value;
        instrument.DirtyRenderer = true;
        Dirty(uid, instrument);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        ShutdownCVars();
    }

    private void OnBoundUIClosed(EntityUid uid, InstrumentComponent component, BoundUIClosedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        if (HasComp<ActiveInstrumentComponent>(uid)
            && _bui.TryGetUi(uid, args.UiKey, out var bui)
            && bui.SubscribedSessions.Count == 0)
        {
            RemComp<ActiveInstrumentComponent>(uid);
        }

        Clean(uid, component);
    }

    private void OnBoundUIOpened(EntityUid uid, InstrumentComponent component, BoundUIOpenedEvent args)
    {
        if (args.UiKey is not InstrumentUiKey)
            return;

        EnsureComp<ActiveInstrumentComponent>(uid);
        Clean(uid, component);
    }

    private void OnBoundUIRequestBands(EntityUid uid, InstrumentComponent component, InstrumentBandRequestBuiMessage args)
    {
        _bandRequestQueue.Enqueue(args);
    }

    public (EntityUid, string)[] GetBands(EntityUid uid)
    {
        var metadataQuery = EntityManager.GetEntityQuery<MetaDataComponent>();

        if (Deleted(uid, metadataQuery))
            return Array.Empty<(EntityUid, string)>();

        var list = new ValueList<(EntityUid, string)>();
        var activeQuery = EntityManager.GetEntityQuery<ActiveInstrumentComponent>();
        var instrumentQuery = EntityManager.GetEntityQuery<InstrumentComponent>();

        foreach (var entity in _lookup.GetEntitiesInRange(uid, MaxInstrumentBandRange))
        {
            if (entity == uid)
                continue;

            if (!activeQuery.HasComponent(entity))
                continue;

            if (!instrumentQuery.TryGetComponent(entity, out var instrument))
                continue;

            // We want to use the instrument player's name.
            if (instrument.InstrumentPlayer?.AttachedEntity is not {} playerUid)
                continue;

            if (!metadataQuery.TryGetComponent(playerUid, out var metadata))
                continue;

            list.Add((entity, metadata.EntityName));
        }

        return list.ToArray();
    }

    public void Clean(EntityUid uid, InstrumentComponent? instrument = null)
    {
        if (!Resolve(uid, ref instrument))
            return;

        if (instrument.Playing)
        {
            RaiseNetworkEvent(new InstrumentStopMidiEvent(uid));
        }

        instrument.Playing = false;
        instrument.Master = null;
        instrument.MasterChannels.SetAll(true);
        instrument.LastSequencerTick = 0;
        instrument.BatchesDropped = 0;
        instrument.LaggedBatches = 0;
        Dirty(uid, instrument);
    }

    private void OnMidiEventRx(InstrumentMidiEventEvent msg, EntitySessionEventArgs args)
    {
        var uid = msg.Uid;

        if (!TryComp(uid, out InstrumentComponent? instrument))
            return;

        if (!instrument.Playing
            || args.SenderSession != instrument.InstrumentPlayer
            || instrument.InstrumentPlayer == null
            || args.SenderSession.AttachedEntity is not {} attached)
            return;

        var send = true;

        var minTick = uint.MaxValue;
        var maxTick = uint.MinValue;

        for (var i = 0; i < msg.MidiEvent.Length; i++)
        {
            var tick = msg.MidiEvent[i].Tick;

            if (tick < minTick)
                minTick = tick;

            if (tick > maxTick)
                maxTick = tick;
        }

        if (instrument.LastSequencerTick > minTick)
        {
            instrument.LaggedBatches++;

            if (instrument.RespectMidiLimits)
            {
                if (instrument.LaggedBatches == (int) (MaxMidiLaggedBatches * (1 / 3d) + 1))
                {
                    _popup.PopupEntity(Loc.GetString("instrument-component-finger-cramps-light-message"),
                        uid, attached, PopupType.SmallCaution);
                }
                else if (instrument.LaggedBatches == (int) (MaxMidiLaggedBatches * (2 / 3d) + 1))
                {
                    _popup.PopupEntity(Loc.GetString("instrument-component-finger-cramps-serious-message"),
                        uid, attached, PopupType.MediumCaution);
                }
            }

            if (instrument.LaggedBatches > MaxMidiLaggedBatches)
            {
                send = false;
            }
        }

        if (++instrument.MidiEventCount > MaxMidiEventsPerSecond
            || msg.MidiEvent.Length > MaxMidiEventsPerBatch)
        {
            instrument.BatchesDropped++;

            send = false;
        }

        instrument.LastSequencerTick = Math.Max(maxTick, minTick);

        if (send || !instrument.RespectMidiLimits)
        {
            RaiseNetworkEvent(msg);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_bandRequestQueue.Count > 0 && _bandRequestTimer < _timing.RealTime)
        {
            while (_bandRequestQueue.TryDequeue(out var request))
            {
                var nearby = GetBands(request.Entity);
                _bui.TrySendUiMessage(request.Entity, request.UiKey, new InstrumentBandResponseBuiMessage(nearby),
                    (IPlayerSession)request.Session);
            }
        }

        var activeQuery = EntityManager.GetEntityQuery<ActiveInstrumentComponent>();
        var metadataQuery = EntityManager.GetEntityQuery<MetaDataComponent>();
        var transformQuery = EntityManager.GetEntityQuery<TransformComponent>();

        var query = AllEntityQuery<ActiveInstrumentComponent, InstrumentComponent>();
        while (query.MoveNext(out var uid, out _, out var instrument))
        {
            if (instrument.DirtyRenderer)
            {
                instrument.DirtyRenderer = false;
                Dirty(uid, instrument);
            }

            if (instrument.Master is {} master)
            {
                if (Deleted(master, metadataQuery))
                {
                    Clean(uid, instrument);
                }

                var masterActive = activeQuery.CompOrNull(master);
                if (masterActive == null)
                {
                    Clean(uid, instrument);
                }

                var trans = transformQuery.GetComponent(uid);
                var masterTrans = transformQuery.GetComponent(master);
                if (!masterTrans.Coordinates.InRange(EntityManager, _transform, trans.Coordinates, 10f))
                {
                    Clean(uid, instrument);
                }
            }

            if (instrument.RespectMidiLimits &&
                (instrument.BatchesDropped >= MaxMidiBatchesDropped
                 || instrument.LaggedBatches >= MaxMidiLaggedBatches))
            {
                if (instrument.InstrumentPlayer?.AttachedEntity is {Valid: true} mob)
                {
                    _stuns.TryParalyze(mob, TimeSpan.FromSeconds(1), true);

                    _popup.PopupEntity(Loc.GetString("instrument-component-finger-cramps-max-message"),
                        uid, mob, PopupType.LargeCaution);
                }

                // Just in case
                Clean(uid);

                if (instrument.UserInterface is not null)
                    _bui.CloseAll(instrument.UserInterface);
            }

            instrument.Timer += frameTime;
            if (instrument.Timer < 1)
                continue;

            instrument.Timer = 0f;
            instrument.MidiEventCount = 0;
            instrument.LaggedBatches = 0;
            instrument.BatchesDropped = 0;
        }
    }

    public void ToggleInstrumentUi(EntityUid uid, IPlayerSession session, InstrumentComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (_bui.TryGetUi(uid, InstrumentUiKey.Key, out var bui))
            _bui.ToggleUi(bui, session);
    }
}
