using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.PackageWrapper.Components
{
    [Serializable, Prototype("WrapShapedType")]
    public class WrapperShapedTypePrototype : IPrototype
    {
        [ViewVariables]
        [DataField("id", required: true)]
        public string ID { get; } = default!;

        [DataField("protospawnid")]
        public string ProtoSpawnID { get; } = string.Empty;

        [DataField("tags")]
        public List<string> Tags = new();
    }
}
