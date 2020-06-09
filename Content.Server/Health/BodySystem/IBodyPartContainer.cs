﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server.BodySystem
{

    /// <summary>
    ///     An empty interface, but making a class inherit from this interface allows you to do many things with it in the <see cref="ISurgeryData">ISurgeryData</see> class. This includes passing
    ///     it as an argument to a <see cref="ISurgeryData.SurgeryAction">SurgeryAction</see> delegate, as to later typecast it back to the original class type. Every BodyPart also needs an
    ///     IBodyPartContainer to be its parent (i.e. the BodyManagerComponent holds many BodyParts, each of which have an upward reference to it).
    /// </summary>

    public interface IBodyPartContainer
    {

    }
}
