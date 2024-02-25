using Content.Server.Popups;
using Content.Server.PowerCell;
using Content.Server.Radio.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.PowerCell.Components;
using Content.Shared.Verbs;

namespace Content.Server.Radio.EntitySystems;

public sealed class JammerSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioJammerComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ActiveRadioJammerComponent, PowerCellChangedEvent>(OnPowerCellChanged);
        SubscribeLocalEvent<RadioJammerComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioJammerComponent, GetVerbsEvent<Verb>>(OnGetVerb);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveRadioJammerComponent, RadioJammerComponent>();

        while (query.MoveNext(out var uid, out var _, out var jam))
        {

            if (_powerCell.TryGetBatteryFromSlot(uid, out var battery) &&
                !battery.TryUseCharge(GetCurrentWattage(jam) * frameTime))
            {
                RemComp<ActiveRadioJammerComponent>(uid);
            }

        }
    }

    private void OnActivate(EntityUid uid, RadioJammerComponent comp, ActivateInWorldEvent args)
    {

        var activated = !HasComp<ActiveRadioJammerComponent>(uid) && 
            _powerCell.TryGetBatteryFromSlot(uid, out var battery) &&
            battery.CurrentCharge > GetCurrentWattage(comp);
        if (activated)
        {
            EnsureComp<ActiveRadioJammerComponent>(uid);
        }
        else
        {
            RemComp<ActiveRadioJammerComponent>(uid);
        }
        var state = Loc.GetString(activated ? "radio-jammer-component-on-state" : "radio-jammer-component-off-state");
        var message = Loc.GetString("radio-jammer-component-on-use", ("state", state));
        _popup.PopupEntity(message, args.User, args.User);
        args.Handled = true;
    }

    private void OnPowerCellChanged(EntityUid uid, ActiveRadioJammerComponent comp, PowerCellChangedEvent args)
    {
        if (args.Ejected)
            RemComp<ActiveRadioJammerComponent>(uid);
    }

    private void OnExamine(EntityUid uid, RadioJammerComponent comp, ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            var msg = HasComp<ActiveRadioJammerComponent>(uid)
                ? Loc.GetString("radio-jammer-component-examine-on-state")
                : Loc.GetString("radio-jammer-component-examine-off-state");
            args.PushMarkup(msg);
        }
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        var source = Transform(args.RadioSource).Coordinates;
        var query = EntityQueryEnumerator<ActiveRadioJammerComponent, RadioJammerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var jam, out var transform))
        {
            if (source.InRange(EntityManager, _transform, transform.Coordinates, GetCurrentRange(jam)))
            {
                args.Cancelled = true;
                return;
            }
        }
    }

    private void OnGetVerb(Entity<RadioJammerComponent> entity, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        
        string[] popUpArray = 
        {
            Loc.GetString("radio-jammer-component-set-low"),
            Loc.GetString("radio-jammer-component-set-medium"),
            Loc.GetString("radio-jammer-component-set-high")
        };
        string[] settingArray = 
        {
            Loc.GetString("radio-jammer-component-low"),
            Loc.GetString("radio-jammer-component-medium"),
            Loc.GetString("radio-jammer-component-high")
        };

        var user = args.User;
        
        for (byte i = 0; i < 3; i++) 
        {
            byte currentPowerLevel = i;
            
            var verb = new Verb
            {
                Priority = currentPowerLevel+1,
                Category = VerbCategory.PowerLevel,
                Disabled = entity.Comp.SelectedPowerLevel == currentPowerLevel,
                Act = () =>
                {
                    entity.Comp.SelectedPowerLevel = currentPowerLevel;
                    _popup.PopupEntity(popUpArray[currentPowerLevel], user, user);
                },
                Text = settingArray[currentPowerLevel],
            };
            args.Verbs.Add(verb);
        }

    }

    private static float GetCurrentWattage(RadioJammerComponent jammer)
    {
        switch(jammer.SelectedPowerLevel)
        {
        case 2:
            return jammer.HighPowerWattage;
        case 1:
            return jammer.MediumPowerWattage;
        default:
            return jammer.LowPowerWattage;
        }
    }
    private static float GetCurrentRange(RadioJammerComponent jammer)
    {
        switch(jammer.SelectedPowerLevel)
        {
        case 2:
            return jammer.HighPowerRange;
        case 1:
            return jammer.MediumPowerRange;
        default:
            return jammer.LowPowerRange;
        };
    }
}
