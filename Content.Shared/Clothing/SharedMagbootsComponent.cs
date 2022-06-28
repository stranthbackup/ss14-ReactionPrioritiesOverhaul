using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Clothing;

[NetworkedComponent()]
public abstract class SharedMagbootsComponent : Component
{
    [DataField("toggleAction", required: true)]
    public InstantAction ToggleAction = new();

    public abstract bool On { get; set; }

    [Serializable, NetSerializable]
    public sealed class MagbootsComponentState : ComponentState
    {
        public bool On { get; }

        public MagbootsComponentState(bool @on)
        {
            On = on;
        }
    }
}
