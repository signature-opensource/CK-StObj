#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObj\DependentItemKindSpec.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Core
{
    /// <summary>
    /// Describes the kind of a dependent item.
    /// Used by container to dynamically restrict its type.
    /// </summary>
    public enum DependentItemKindSpec
    {
        /// <summary>
        /// Unknown type can be used for instance to dynamically adjust the behavior of the item.
        /// </summary>
        Unknown,

        /// <summary>
        /// Considers the item as a pure dependent item.
        /// </summary>
        Item,

        /// <summary>
        /// Considers the item as a group.
        /// </summary>
        Group,

        /// <summary>
        /// Considers the item as a container.
        /// </summary>
        Container
    }

}
