#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\AutoImplementor\ImplementableAbstractPropertyInfo.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Associates an <see cref="IAutoImplementorProperty"/> to use for a <see cref="Property"/>
    /// that is abstract or virtual.
    /// </summary>
    public readonly struct ImplementablePropertyInfo
    {
        internal ImplementablePropertyInfo( PropertyInfo p, IAutoImplementorProperty impl )
        {
            Property = p;
            ImplementorToUse = impl;
        }

        /// <summary>
        /// Abstract property that has to be automatically implemented
        /// or virtual property that can be overridden.
        /// </summary>
        public readonly PropertyInfo Property;

        /// <summary>
        /// The <see cref="IAutoImplementorProperty"/> to use.
        /// </summary>
        public readonly IAutoImplementorProperty ImplementorToUse;
    }

}
