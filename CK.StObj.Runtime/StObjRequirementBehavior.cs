#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\StObjRequirementBehavior.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the <see cref="IStObjMutableReference.StObjRequirementBehavior"/> values.
    /// </summary>
    public enum StObjRequirementBehavior
    {
        /// <summary>
        /// The reference is not necessarily an existing <see cref="IRealObject"/> (a <see cref="IStObjResult"/>).
        /// if an existing IStObj can not be found, the <see cref="IStObjValueResolver"/> is automatically sollicited.
        /// </summary>
        None = 0,

        /// <summary>
        /// A warn is emitted if the reference is not a <see cref="IStObjResult"/>, and the <see cref="IStObjValueResolver"/>
        /// is sollicited.
        /// </summary>
        WarnIfNotStObj,

        /// <summary>
        /// The reference must be an existing <see cref="IRealObject"/> (a <see cref="IStObjResult"/>).
        /// </summary>
        ErrorIfNotStObj,

        /// <summary>
        /// The reference must be satisfied only by <see cref="IStObjValueResolver"/>. 
        /// Any existing <see cref="IRealObject"/> (a <see cref="IStObjResult"/>) that could do the job are ignored.
        /// </summary>
        ExternalReference
    }
}
