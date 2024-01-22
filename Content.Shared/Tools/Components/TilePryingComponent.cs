using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Tools.Components;

/// <summary>
/// Allows prying tiles up on a grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TilePryingComponent : Component
{
    [DataField("toolComponentNeeded"), AutoNetworkedField]
    public bool ToolComponentNeeded = true;

    [DataField("qualityNeeded", customTypeSerializer:typeof(PrototypeIdSerializer<ToolQualityPrototype>)), AutoNetworkedField]
    public string QualityNeeded = "Prying";

    [DataField("delay"), AutoNetworkedField]
    public float Delay = 1f;
}
