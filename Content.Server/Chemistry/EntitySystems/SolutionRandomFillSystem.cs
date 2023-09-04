using Content.Server.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Chemistry.EntitySystems;

[InjectDependencies]
public sealed partial class SolutionRandomFillSystem : EntitySystem
{
    [Dependency] private SolutionContainerSystem _solutionsSystem = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomFillSolutionComponent, MapInitEvent>(OnRandomSolutionFillMapInit);
    }

    private void OnRandomSolutionFillMapInit(EntityUid uid, RandomFillSolutionComponent component, MapInitEvent args)
    {
        var target = _solutionsSystem.EnsureSolution(uid, component.Solution);
        var pick = _proto.Index<WeightedRandomFillSolutionPrototype>(component.WeightedRandomId).Pick(_random);

        var reagent = pick.reagent;
        var quantity = pick.quantity;

        if (!_proto.HasIndex<ReagentPrototype>(reagent))
        {
            Log.Error($"Tried to add invalid reagent Id {reagent} using SolutionRandomFill.");
            return;
        }

        target.AddReagent(reagent, quantity);
    }
}
