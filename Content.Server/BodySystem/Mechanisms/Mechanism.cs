﻿using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using YamlDotNet.RepresentationModel;




namespace Robust.Shared.BodySystem {
    public enum MechanismType { passive, toggle, active }

    [Prototype("mechanism")]
	public class Mechanism {
		private string _name;
		private string _description;
		private string _examineMessage;
		private int _durability;
		private int _currentDurability;
		private int _destroyThreshold;
		private int _resistance;
		private int _size;
		private BodyPartCompatability _compatability;
		private MechanismType _type;
		private List<IMechanismEffect> _onTick;
		private List<IMechanismEffect> _onInstall;
		private List<IMechanismEffect> _onUninstall;
		private List<IMechanismEffect> _onToggleOn;
		private List<IMechanismEffect> _onToggleOff;
		private List<IMechanismEffect> _onActivate;
		private List<IMechanismEffect> _onDestroy;
		private List<IMechanismEffect> _onBreak;
		private List<IMechanismEffect> _onRepair;
		
		[ViewVariables]
        public string Name => _name;
		
		/// <summary>
		///     Description shown in a mechanism installation console or when examining an uninstalled mechanism.
		/// </summary>				
		[ViewVariables]
        public string Description => _description;		
		
		/// <summary>
		///     The message to display upon examining a BodyPart with this mechanism installed. If the string is empty (""), no message will be displayed.  
		///     For instance, you shouldn't be able to see a person's heart after glancing at their torso. But you can see that giant battery sticking out of their back.
		/// </summary>			  
		[ViewVariables]
		public string ExamineMessage => _examineMessage;

        /// <summary>
        ///     Max HP of this mechanism.
        /// </summary>		
		[ViewVariables]
		public int Durability => _durability;
		
        /// <summary>
        ///     Current HP of this mechanism.
        /// </summary>		
		[ViewVariables]
		public int CurrentDurability => _currentDurability;
		
		/// <summary>
        ///     At what HP this mechanism is completely destroyed.
        /// </summary>		
		[ViewVariables]
		public int DestroyThreshold => _destroyThreshold;	
		
        /// <summary>
        ///     Armor of this mechanism against attacks.
        /// </summary>		
		[ViewVariables]
		public int Resistance => _resistance;
		
        /// <summary>
        ///     Determines a handful of things - mostly whether this mechanism can fit into a BodyPart.
        /// </summary>		
		[ViewVariables]
		public int Size => _size;
		
        /// <summary>
        ///     What kind of BodyParts this mechanism can be installed into.
        /// </summary>
        [ViewVariables]
        public BodyPartCompatability Compatability => _compatability;
		
        /// <summary>
        ///     How this mechanism works - active, passive, or toggleable.
        /// </summary>		
		[ViewVariables]
		public MechanismType Type => _type;
		
		
        public virtual void LoadFrom(YamlMappingNode mapping){
            var serializer = YamlObjectSerializer.NewReader(mapping);

            serializer.DataField(ref _name, "name", string.Empty);		
		}		
	}
}

