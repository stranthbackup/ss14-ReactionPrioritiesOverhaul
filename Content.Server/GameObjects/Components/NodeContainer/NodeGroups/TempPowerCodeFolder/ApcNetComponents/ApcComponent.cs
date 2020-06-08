﻿using Content.Server.GameObjects.Components.NodeContainer.NodeGroups;
using Robust.Shared.GameObjects;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.NewPower.ApcNetComponents
{
    [RegisterComponent]
    public class ApcComponent : BaseApcNetComponent
    {
        public override string Name => "NewApc";

        [ViewVariables]
        public BatteryComponent Battery { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            Battery = Owner.GetComponent<BatteryComponent>();
        }

        protected override void AddSelfToNet(IApcNet apcNet)
        {
            apcNet.AddApc(this);
        }

        protected override void RemoveSelfFromNet(IApcNet apcNet)
        {
            apcNet.RemoveApc(this);
        }
    }
}
