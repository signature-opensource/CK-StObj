#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\PropertyResolutionSource.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Core
{
    /// <summary>
    /// Defines how a property value will be searched if not set explicitly set.
    /// </summary>
    public enum PropertyResolutionSource
    {
        /// <summary>
        /// Property is not resolved from container nor generalization.
        /// This should be rarely used.
        /// </summary>
        None,

        /// <summary>
        /// Property is resolved first from the Container and, if not found, from the Generalization.
        /// This is the default for <see cref="StObjPropertyAttribute">StObj Properties</see>.
        /// </summary>
        FromContainerAndThenGeneralization,

        /// <summary>
        /// Property is resolved first from the Generalization and, if not found, from its Containers.
        /// This is the default for <see cref="AmbientPropertyAttribute">AmbientProperty</see>.
        /// </summary>
        FromGeneralizationAndThenContainer
    }
}
