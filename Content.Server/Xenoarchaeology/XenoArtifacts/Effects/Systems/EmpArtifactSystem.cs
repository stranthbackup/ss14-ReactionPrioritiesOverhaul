using Content.Server.Emp;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Events;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

[InjectDependencies]
public sealed partial class EmpArtifactSystem : EntitySystem
{
    [Dependency] private EmpSystem _emp = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<EmpArtifactComponent, ArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, EmpArtifactComponent component, ArtifactActivatedEvent args)
    {
        _emp.EmpPulse(Transform(uid).MapPosition, component.Range, component.EnergyConsumption, component.DisableDuration);
    }
}
