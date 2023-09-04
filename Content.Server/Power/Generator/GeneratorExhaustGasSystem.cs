﻿using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Power.Generator;

namespace Content.Server.Power.Generator;

/// <seealso cref="GeneratorSystem"/>
/// <seealso cref="GeneratorExhaustGasComponent"/>
[InjectDependencies]
public sealed partial class GeneratorExhaustGasSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneratorExhaustGasComponent, GeneratorUseFuel>(FuelUsed);
    }

    private void FuelUsed(EntityUid uid, GeneratorExhaustGasComponent component, GeneratorUseFuel args)
    {
        var exhaustMixture = new GasMixture();
        exhaustMixture.SetMoles(component.GasType, args.FuelUsed * component.MoleRatio);
        exhaustMixture.Temperature = component.Temperature;

        var environment = _atmosphere.GetContainingMixture(uid, false, true);
        if (environment != null)
            _atmosphere.Merge(environment, exhaustMixture);
    }
}
