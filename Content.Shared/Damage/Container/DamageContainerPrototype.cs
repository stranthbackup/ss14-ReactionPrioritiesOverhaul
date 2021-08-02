using System;
using System.Collections.Generic;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.Damage.Container
{
    /// <summary>
    ///     Prototype for the DamageContainer class.
    /// </summary>
    [Prototype("damageContainer")]
    [Serializable, NetSerializable]
    public class DamageContainerPrototype : IPrototype, ISerializationHooks
    {
        private IPrototypeManager _prototypeManager = default!;

        [ViewVariables]
        [DataField("id", required: true)]
        public string ID { get; } = default!;

        /// <summary>
        /// Determines whether this DamageContainerPrototype will support ALL damage types and groups. If true, ignore
        /// all other options.
        /// </summary>
        [DataField("supportAll")] private bool _supportAll;

        [DataField("supportedGroups")] private HashSet<string> _supportedDamageGroupIDs = new();
        [DataField("supportedTypes")] private HashSet<string> _supportedDamageTypeIDs = new();

        private HashSet<DamageGroupPrototype> _applicableDamageGroups = new();
        private HashSet<DamageGroupPrototype> _supportedDamageGroups = new();
        private HashSet<DamageTypePrototype> _supportedDamageTypes = new();

        // TODO NET 5 IReadOnlySet

        /// <summary>
        /// Collection of damage groups that can affect this container.
        /// </summary>
        /// <remarks>
        /// This describes what damage groups can have an effect on this damage container. However not every damage
        /// group has to be fully supported. For example, the container may support ONLY the piercing damage type. It
        /// should therefore be affected by instances of brute group damage, but does not neccesarily support blunt or slash
        /// damage. If damage containers are only specified by supported damage groups, and every damage type is in only
        /// one damage group, then SupportedDamageTypes should be equal to ApplicableDamageGroups. For a list of
        /// supported damage types, see <see cref="SupportedDamageTypes"/>.
        /// </remarks>
        [ViewVariables] public IReadOnlyCollection<DamageGroupPrototype> ApplicableDamageGroups => _applicableDamageGroups;

        /// <summary>
        /// Collection of damage groups that are fully supported by this container.
        /// </summary>
        /// <remarks>
        /// This describes what damage groups this damage container explicitly supports. It supports every damage type
        /// contained in these damage groups. It may also support other damage types not in these groupps. To see all
        /// damage types <see cref="SupportedDamageTypes"/>, and to see all applicable damage groups <see
        /// cref="ApplicableDamageGroups"/>.
        /// </remarks>
        [ViewVariables] public IReadOnlyCollection<DamageGroupPrototype> SupportedDamageGroups => _supportedDamageGroups;

        /// <summary>
        /// Collection of damage types supported by this container.
        /// </summary>
        /// <remarks>
        /// Each of these damage types is fully supported by the DamageContainer. If any of these damage types is a
        /// member of a damage group, these groups are added to <see cref="ApplicableDamageGroups"></see>
        /// </remarks>
        [ViewVariables] public IReadOnlyCollection<DamageTypePrototype> SupportedDamageTypes => _supportedDamageTypes;

        void ISerializationHooks.AfterDeserialization()
        {
            _prototypeManager = IoCManager.Resolve<IPrototypeManager>();

            // If all damge types are ALL supported, add all of them.
            if (_supportAll)
            {
                foreach (var group in _prototypeManager.EnumeratePrototypes<DamageGroupPrototype>())
                {
                    _applicableDamageGroups.Add(group);
                    _supportedDamageGroups.Add(group);
                }
                foreach (var type in _prototypeManager.EnumeratePrototypes<DamageTypePrototype>())
                {
                    _supportedDamageTypes.Add(type);
                }
                return;
            }

            // Add fully supported damage groups
            foreach (var groupID in _supportedDamageGroupIDs)
            {
                var group = _prototypeManager.Index<DamageGroupPrototype>(groupID);
                _supportedDamageGroups.Add(group);
                foreach (var type in group.DamageTypes)
                {
                    _supportedDamageTypes.Add(type);
                }
            }

            // Add individual damage types, that are either not part of a group, or whose groups are not fully supported
            foreach (var supportedTypeID in _supportedDamageTypeIDs)
            {
                var type = _prototypeManager.Index<DamageTypePrototype>(supportedTypeID);
                _supportedDamageTypes.Add(type);
            }

            // For each supported damage type, check whether it is in any existing group, and add it to _applicableDamageGroups
            foreach (var type in _supportedDamageTypes)
            {
                foreach (var group in _prototypeManager.EnumeratePrototypes<DamageGroupPrototype>())
                {
                    if (group.DamageTypes.Contains(type))
                    {
                        _applicableDamageGroups.Add(group);
                    }
                }
            }
        }
    }
}
