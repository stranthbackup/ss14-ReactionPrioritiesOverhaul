using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Strip.Components;

/// <summary>
/// Give this to an entity when you want to decrease stripping times
/// </summary>
[RegisterComponent]
public sealed partial class ThievingComponent : Component
{
    /// <summary>
    /// How much the strip time should be shortened by
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("stripTimeReduction")]
    public float StripTimeReduction = 0.5f;

    /// <summary>
    /// Should it notify the user if they're stripping a pocket?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("stealthy")]
    public bool Stealthy;
}

[RegisterComponent]
public sealed partial class ToggleableThievingComponent : Component
{

    [DataField]
    public EntProtoId ThievingToggleActionProto = "ActionToggleThieving";

    [DataField]
    public EntityUid? ThievingToggleAction;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public bool Stealthy = true;

    [DataField, ViewVariables(VVAccess.ReadWrite)]

    public float StripTimeReduction = 0;
}

public sealed partial class ToggleThievingActionEvent : InstantActionEvent
{

}
