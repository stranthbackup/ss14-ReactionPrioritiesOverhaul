using Content.Server.Charges.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server.Charges.Systems;

public sealed class ChargesSystem : SharedChargesSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoRechargeComponent, EntityUnpausedEvent>(OnUnpaused);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (charges, recharge) in EntityQuery<LimitedChargesComponent, AutoRechargeComponent>())
        {
            if (charges.Charges == charges.MaxCharges || _timing.CurTime < recharge.NextChargeTime)
                continue;

            AddCharges(charges, 1);
            recharge.NextChargeTime = _timing.CurTime + recharge.RechargeDuration;
        }
    }

    private void OnUnpaused(EntityUid uid, AutoRechargeComponent comp, ref EntityUnpausedEvent args)
    {
        comp.NextChargeTime += args.PausedTime;
    }

    protected override void OnExamine(EntityUid uid, LimitedChargesComponent comp, ExaminedEvent args)
    {
        // only show the recharging info if it's not full
        if (!args.IsInDetailsRange || comp.Charges == comp.MaxCharges || !TryComp<AutoRechargeComponent>(uid, out var recharge))
            return;

        var timeRemaining = Math.Round((recharge.NextChargeTime - _timing.CurTime).TotalSeconds);
        args.PushMarkup(Loc.GetString("limited-charges-recharging", ("seconds", timeRemaining)));
    }

    public override void UseCharge(EntityUid uid, LimitedChargesComponent comp)
    {
        var startRecharge = comp.Charges == comp.MaxCharges;
        base.UseCharge(uid, comp);
        // start the recharge time after first use at full charge
        if (startRecharge && TryComp<AutoRechargeComponent>(uid, out var recharge))
            recharge.NextChargeTime = _timing.CurTime + recharge.RechargeDuration;
    }
}
