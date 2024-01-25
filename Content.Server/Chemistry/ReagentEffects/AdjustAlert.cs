﻿using Content.Shared.Alert;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Chemistry.ReagentEffects;

public sealed partial class AdjustAlert : ReagentEffect
{
    [DataField("alertType", required: true)]
    public AlertType Type;

    [DataField]
    public bool Clear;

    // Show cooldown progress over the alert
    [DataField]
    public bool Cooldown;

    // Automatically remove the alert after a set time
    [DataField]
    public bool AutoRemove;

    // The length of the cooldown or the delay before autoRemove (in seconds) .
    [DataField]
    public float Time;

    //JUSTIFICATION: This just changes some visuals, doesn't need to be documented.
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    public override void Effect(ReagentEffectArgs args)
    {
        var alertSys = EntitySystem.Get<AlertsSystem>();
        if (!args.EntityManager.HasComponent<AlertsComponent>(args.SolutionEntity))
            return;

        if (Clear)
        {
                alertSys.ClearAlert(args.SolutionEntity, Type);
        }
        else
        {
            var timing = IoCManager.Resolve<IGameTiming>();
            (TimeSpan, TimeSpan)? cooldown = null;
            TimeSpan? autoRemove = null;

            if (Cooldown)
            {
                cooldown = (timing.CurTime, timing.CurTime + TimeSpan.FromSeconds(Time));
            }

            if (AutoRemove)
            {
                autoRemove = (timing.CurTime + TimeSpan.FromSeconds(Time));
            }
            alertSys.ShowAlert(args.SolutionEntity, Type, cooldown: cooldown, autoRemove: autoRemove);
        }

    }
}
