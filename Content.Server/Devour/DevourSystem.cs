using Content.Shared.Devour;
using Content.Server.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Chemistry.Components;
using Content.Server.Devour.Components;

namespace Content.Server.Devour;

public sealed class DevourSystem : SharedDevourSystem
{
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DevourerComponent, DevourActionEvent>(OnDevourAction);
        SubscribeLocalEvent<DevourerComponent, DevourDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(EntityUid uid, DevourerComponent component, DevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var ichorInjection = new Solution(component.Chemical, component.HealRate);

        if (component.FoodPreference == FoodPreference.All ||
            (component.FoodPreference == FoodPreference.Humanoid && HasComp<HumanoidAppearanceComponent>(args.Args.Target)))
        {
            ichorInjection.ScaleSolution(0.5f);

            if (component.ShouldStoreDevoured && args.Args.Target is not null)
            {
                component.Stomach.Insert(args.Args.Target.Value);
            }
            _bloodstreamSystem.TryAddToChemicals(uid, ichorInjection);
        }

        //TODO: Figure out a better way of removing structures via devour that still entails standing still and waiting for a DoAfter. Somehow.
        //If it's not human, it must be a structure
        else if (args.Args.Target != null)
            EntityManager.QueueDeleteEntity(args.Args.Target.Value);

        _audioSystem.PlayPvs(component.SoundDevour, uid);
    }
}
