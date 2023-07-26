using Content.Shared.StepTrigger.Systems;
using Content.Shared.StepTrigger.Components;
using Content.Shared.FootPrints;
using Content.Shared.Fluids;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
//using Content.Server.Explosion.EntitySystems;


namespace Content.Server.FootPrints
{
    public sealed class PuddleFootPrintsSystem : EntitySystem
    {
        //[Dependency] private readonly TriggerSystem _trigger = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PuddleFootPrintsComponent, EndCollideEvent>(OnStepTrigger);
        }

        public void OnStepTrigger(EntityUid uid, PuddleFootPrintsComponent comp, ref EndCollideEvent args)
        {
            if(!TryComp<AppearanceComponent>(uid, out var appearance))
                return;
            if(!TryComp<FootPrintsComponent>(args.OtherEntity, out var tripper))
                return;
            if(_appearance.TryGetData(uid,PuddleVisuals.SolutionColor, out var color, appearance))
            {
                tripper.PrintsColor = (Color)color;
            }

        }
    }
}
