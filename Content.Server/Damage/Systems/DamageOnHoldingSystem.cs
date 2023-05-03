using Content.Server.Damage.Components;
using Content.Shared.Damage;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Damage.Systems;

public sealed class DamageOnHoldingSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public void SetEnabled(EntityUid uid, bool enabled)
    {
        if (TryComp<DamageOnHoldingComponent>(uid, out var component))
            component.Enabled = enabled;
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DamageOnHoldingComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!component.Enabled || component.NextDamage > _timing.CurTime)
                continue;
            if (_container.TryGetContainingContainer(uid, out var container))
            {
                _damageableSystem.TryChangeDamage(container.Owner, component.Damage, origin: uid);
            }
            component.NextDamage += TimeSpan.FromSeconds(component.Interval);
            if (component.NextDamage < _timing.CurTime) // on first iteration or if component was disabled for long time
                component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(component.Interval);
        }
    }
}