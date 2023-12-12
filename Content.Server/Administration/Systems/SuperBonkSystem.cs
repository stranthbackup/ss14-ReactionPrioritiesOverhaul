using Content.Server.Administration.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Climbing.Systems;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server.Administration.Systems;

public sealed class SuperBonkSystem: EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly BonkSystem _bonkSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperBonkComponent, ComponentShutdown>(OnBonkShutdown);
    }

    public void StartSuperBonk(EntityUid target, float delay = 0.1f, bool stopWhenDead = false )
    {

        var hadClumsy = EnsureComp<ClumsyComponent>(target, out _);

        var tables = EntityQueryEnumerator<BonkableComponent>();
        var bonks = new Dictionary<EntityUid, BonkableComponent>();
        // This is done so we don't crash if something like a new table is spawned.
        while (tables.MoveNext(out var uid, out var comp))
        {
            bonks.Add(uid, comp);
        }

        var sComp = new SuperBonkComponent
        {
            Target = target,
            Tables = bonks.GetEnumerator(),
            RemoveClumsy = !hadClumsy,
            StopWhenDead = stopWhenDead,
        };

        AddComp(target, sComp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var comps = EntityQueryEnumerator<SuperBonkComponent>();

        while (comps.MoveNext(out var uid, out var comp))
        {
            comp.TimeRemaining -= frameTime;
            if (!(comp.TimeRemaining <= 0))
                continue;

            Bonk(comp);

            if (!(comp.Tables.MoveNext()))
            {
                RemComp<SuperBonkComponent>(comp.Target);
                continue;
            }

            if (comp.StopWhenDead)
            {
                if (TryComp<MobStateComponent>(comp.Target, out var mComp)
                    && mComp.CurrentState == MobState.Dead)
                {
                    RemComp<SuperBonkComponent>(comp.Target);
                    continue;
                }
            }

            comp.TimeRemaining = comp.InitialTime;
        }
    }

    public void Bonk(SuperBonkComponent comp)
    {
        var uid = comp.Tables.Current.Key;
        var bonkComp = comp.Tables.Current.Value;

        // It would be very weird for something without a transform component to have a bonk component
        // but just in case because I don't want to crash the server.
        if (!HasComp<TransformComponent>(uid))
            return;

        _transformSystem.SetCoordinates(comp.Target, Transform(uid).Coordinates);

        //This is just so glass tables shatter which is funnier.
        if (HasComp<GlassTableComponent>(uid))
        {
            var ev = new ClimbedOnEvent(comp.Target, comp.Target);
            RaiseLocalEvent(uid, ref ev);
            return;
        }

        _bonkSystem.TryBonk(comp.Target, uid, bonkComp);
    }

    private void OnBonkShutdown(EntityUid uid, SuperBonkComponent comp, ComponentShutdown ev)
    {
        if (comp.RemoveClumsy)
            RemComp<ClumsyComponent>(comp.Target);
    }
}
