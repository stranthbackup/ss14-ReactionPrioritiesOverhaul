﻿#nullable enable
using Content.Shared.GameObjects.Components.Body.Mechanism;
using Content.Shared.GameObjects.Components.Body.Part;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Shared.GameObjects.Components.Body.Surgery
{
    /// <summary>
    ///     This data class represents the state of a <see cref="IBodyPart"/> in
    ///     regards to everything surgery related - whether there's an incision on
    ///     it, whether the bone is broken, etc.
    /// </summary>
    public abstract class SurgeryData
    {
        protected delegate void SurgeryAction(IBodyPartContainer container, ISurgeon surgeon, IEntity performer);

        /// <summary>
        ///     The <see cref="IBodyPart"/> this <see cref="SurgeryData"/> is
        ///     attached to.
        /// </summary>
        protected readonly IBodyPart Parent;

        protected SurgeryData(IBodyPart parent)
        {
            Parent = parent;
        }

        /// <summary>
        ///     The <see cref="BodyPartType"/> of the parent
        ///     <see cref="IBodyPart"/>.
        /// </summary>
        protected BodyPartType ParentType => Parent.PartType;

        /// <summary>
        ///     Returns the description of this current <see cref="IBodyPart"/> to
        ///     be shown upon observing the given entity.
        /// </summary>
        public abstract string GetDescription(IEntity target);

        /// <summary>
        ///     Returns whether a <see cref="IMechanism"/> can be installed into the
        ///     <see cref="IBodyPart"/> this <see cref="SurgeryData"/> represents.
        /// </summary>
        public abstract bool CanInstallMechanism(IMechanism mechanism);

        /// <summary>
        ///     Returns whether the given <see cref="IBodyPart"/> can be connected
        ///     to the <see cref="IBodyPart"/> this <see cref="SurgeryData"/>
        ///     represents.
        /// </summary>
        public abstract bool CanAttachBodyPart(IBodyPart part);

        /// <summary>
        ///     Gets the delegate corresponding to the surgery step using the given
        ///     <see cref="SurgeryType"/>.
        /// </summary>
        /// <returns>
        ///     The corresponding surgery action or null if no step can be
        ///     performed.
        /// </returns>
        protected abstract SurgeryAction? GetSurgeryStep(SurgeryType toolType);

        /// <summary>
        ///     Returns whether the given <see cref="SurgeryType"/> can be used to
        ///     perform a surgery on the <see cref="IBodyPart"/> this
        ///     <see cref="SurgeryData"/> represents.
        /// </summary>
        public bool CheckSurgery(SurgeryType toolType)
        {
            return GetSurgeryStep(toolType) != null;
        }

        /// <summary>
        ///     Attempts to perform surgery of the given <see cref="SurgeryType"/>.
        /// </summary>
        /// <param name="surgeryType">
        ///     The <see cref="SurgeryType"/> used for this surgery.
        /// </param>
        /// <param name="container">
        ///     The container where the surgery is being done.
        /// </param>
        /// <param name="surgeon">
        ///     The entity being used to perform the surgery.
        /// </param>
        /// <param name="performer">The entity performing the surgery.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public bool PerformSurgery(SurgeryType surgeryType, IBodyPartContainer container, ISurgeon surgeon,
            IEntity performer)
        {
            var step = GetSurgeryStep(surgeryType);

            if (step == null)
            {
                return false;
            }

            step(container, surgeon, performer);
            return true;
        }
    }
}
