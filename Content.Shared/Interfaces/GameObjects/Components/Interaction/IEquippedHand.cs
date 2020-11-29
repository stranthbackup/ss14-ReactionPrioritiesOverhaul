﻿using System;
using Content.Shared.GameObjects.Components.Inventory;
using Content.Shared.GameObjects.Components.Items;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Shared.Interfaces.GameObjects.Components
{
    /// <summary>
    ///     This interface gives components behavior when their owner is put in a hand inventory slot,
    ///     even if it came from another hand slot (which would also fire <see cref="IUnequippedHand"/>).
    ///     This includes moving the owner from a non-hand slot into a hand slot
    ///     (which would also fire <see cref="IUnequipped"/>).
    /// </summary>
    public interface IEquippedHand
    {
        void EquippedHand(EquippedHandEventArgs eventArgs);
    }

    public class EquippedHandEventArgs : EventArgs
    {
        public EquippedHandEventArgs(IEntity user, SharedHand hand)
        {
            User = user;
            Hand = hand;
        }

        public IEntity User { get; }
        public SharedHand Hand { get; }
    }

    /// <summary>
    ///     Raised when putting the entity into a hand slot
    /// </summary>
    [PublicAPI]
    public class EquippedHandMessage : EntitySystemMessage
    {
        /// <summary>
        ///     If this message has already been "handled" by a previous system.
        /// </summary>
        public bool Handled { get; set; }

        /// <summary>
        ///     Entity that equipped the item.
        /// </summary>
        public IEntity User { get; }

        /// <summary>
        ///     Item that was equipped.
        /// </summary>
        public IEntity Equipped { get; }

        /// <summary>
        ///     Hand the item is going into.
        /// </summary>
        public SharedHand Hand { get; }

        public EquippedHandMessage(IEntity user, IEntity equipped, SharedHand hand)
        {
            User = user;
            Equipped = equipped;
            Hand = hand;
        }
    }
}
