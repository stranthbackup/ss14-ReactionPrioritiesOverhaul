using Robust.Shared.GameStates;

namespace Content.Shared.Slippery;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlidingComponent : Component
{
    /// <summary>
    ///     A list of SuperSlippery entities the entity with this component is colliding with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> CollidingEntities = new ();

    /// <summary>
    ///     The friction modifier that will be applied to any friction calculations.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FrictionModifier;
}
