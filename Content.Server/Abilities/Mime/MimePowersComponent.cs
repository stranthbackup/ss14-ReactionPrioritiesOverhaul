using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Utility;

namespace Content.Server.Abilities.Mime
{
    /// <summary>
    /// Lets its owner entity use mime powers, like placing invisible walls.
    /// </summary>
    [RegisterComponent]
    public sealed class MimePowersComponent : Component
    {
        /// <summary>
        /// Whether this component is active or not.
        /// </summarY>
        [ViewVariables]
        [DataField("enabled")]
        public bool Enabled = true;

        /// <summary>
        /// The wall prototype to use.
        /// </summary>
        [DataField("wallPrototype")]
        public string WallPrototype = "WallInvisible";

        [DataField("invisibleWallAction")]
        public InstantAction InvisibleWallAction = new()
        {
            UseDelay = TimeSpan.FromSeconds(30),
            Icon = new SpriteSpecifier.Texture(new ResourcePath("Structures/Walls/solid.rsi/full.png")),
            Name = "mime-invisible-wall",
            Description = "mime-invisible-wall-desc",
            Priority = -1,
            Event = new InvisibleWallActionEvent(),
        };

        /// <summary>
        /// Whether this mime is ready to take the vow again.
        /// Note that if they already have the vow, this is also false.
        /// </summary>
        public bool ReadyToRepent = false;
    }
}
