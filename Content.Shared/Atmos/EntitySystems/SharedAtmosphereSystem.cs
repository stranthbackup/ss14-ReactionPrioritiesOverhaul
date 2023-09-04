using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Atmos.EntitySystems
{
    [InjectDependencies]
    public abstract partial class SharedAtmosphereSystem : EntitySystem
    {
        [Dependency] private IPrototypeManager _prototypeManager = default!;

        protected readonly GasPrototype[] GasPrototypes = new GasPrototype[Atmospherics.TotalNumberOfGases];

        public override void Initialize()
        {
            base.Initialize();

            for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
            {
                GasPrototypes[i] = _prototypeManager.Index<GasPrototype>(i.ToString());
            }
        }

        public GasPrototype GetGas(int gasId) => GasPrototypes[gasId];

        public GasPrototype GetGas(Gas gasId) => GasPrototypes[(int) gasId];

        public IEnumerable<GasPrototype> Gases => GasPrototypes;
    }
}
