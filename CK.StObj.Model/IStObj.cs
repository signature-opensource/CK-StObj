#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\IStObj.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Core
{
    /// <summary>
    /// Base interface that describes an "object slice".
    /// </summary>
    public interface IStObj
    {
        /// <summary>
        /// Gets the class type of this "slice" of the object.
        /// </summary>
        Type ClassType { get; }

        /// <summary>
        /// Gets the StObj map to which this StObj belongs.
        /// </summary>
        IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets the parent <see cref="IStObj"/> in the inheritance chain (the one associated to the base class of this <see cref="ClassType"/>).
        /// May be null.
        /// </summary>
        IStObj Generalization { get; }

        /// <summary>
        /// Gets the child <see cref="IStObj"/> in the inheritance chain.
        /// Null when this is the <see cref="FinalImplementation"/>.
        /// </summary>
        IStObj Specialization { get; }

        /// <summary>
        /// Gets the final implementation (the most specialized type).
        /// </summary>
        IStObjFinalImplementation FinalImplementation { get; }
    }
}
