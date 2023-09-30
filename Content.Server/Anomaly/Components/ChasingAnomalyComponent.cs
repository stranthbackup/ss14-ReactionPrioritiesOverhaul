using Content.Server.Anomaly.Effects;

namespace Content.Server.Anomaly.Components;


/// <summary>
/// This component allows the anomaly to chase a random instance of the selected type component within a radius.
/// </summary>
[RegisterComponent, Access(typeof(ChasingAnomalySystem))]
public sealed partial class ChasingAnomalyComponent : Component
{
    /// <summary>
    /// The maximum radius in which the anomaly chooses the target component to follow
    /// scales with severity
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxChaseRadius = 10;

    /// <summary>
    /// The speed at which the anomaly is moving towards the target.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float MaxChasingSpeed = 1f;

    /// <summary>
    /// Modification of the chasing speed during the transition to a supercritical state
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float SuperCriticalSpeedModifier = 3;

    /// <summary>
    /// The component that the anomaly is chasing
    /// </summary>
    [DataField(required: true), ViewVariables(VVAccess.ReadOnly)]
    public string ChasingComponent = default!;

    //In Game Storage Variables

    /// <summary>
    /// The entity uid, chasing by the anomaly
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? ChasingEntity;

    /// <summary>
    /// Current movement speed. changes after each pulse depending on severity
    /// scales with severity
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float CurrentSpeed;
}
