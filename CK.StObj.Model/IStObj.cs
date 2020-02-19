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
        /// Gets the associated type (the "slice" of the object).
        /// </summary>
        Type ObjectType { get; }

        /// <summary>
        /// Gets the StObj map to which this StObj belongs.
        /// </summary>
        IStObjMap StObjMap { get; }

        /// <summary>
        /// Gets the parent <see cref="IStObj"/> in the inheritance chain (the one associated to the base class of this <see cref="ObjectType"/>).
        /// May be null.
        /// </summary>
        IStObj Generalization { get; }

        /// <summary>
        /// Gets the child <see cref="IStObj"/> in the inheritance chain.
        /// May be null.
        /// </summary>
        IStObj Specialization { get; }
    }
}
