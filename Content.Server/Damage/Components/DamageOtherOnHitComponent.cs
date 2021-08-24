using Content.Shared.Damage;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;

namespace Content.Server.Damage.Components
{
    [Friend(typeof(DamageOtherOnHitSystem))]
    [RegisterComponent]
    public class DamageOtherOnHitComponent : Component
    {
        public override string Name => "DamageOtherOnHit";

        [DataField("amount")]
        private int _amount = 1;

        [DataField("ignoreResistances")]
<<<<<<< refs/remotes/origin/master
        public bool IgnoreResistances { get; } = false;
=======
        private bool _ignoreResistances;

        // TODO PROTOTYPE Replace this datafield variable with prototype references, once they are supported.
        // Also remove Initialize override, if no longer needed.
        [DataField("damageType")]
        private readonly string _damageTypeID = "Blunt";
        private DamageTypePrototype _damageType = default!;
        protected override void Initialize()
        {
            base.Initialize();
            _damageType = IoCManager.Resolve<IPrototypeManager>().Index<DamageTypePrototype>(_damageTypeID);
        }

        void IThrowCollide.DoHit(ThrowCollideEventArgs eventArgs)
        {
            if (!eventArgs.Target.TryGetComponent(out IDamageableComponent? damageable))
                return;
            damageable.TryChangeDamage(_damageType, _amount, _ignoreResistances);
        }
>>>>>>> update damagecomponent across shared and server
    }
}
