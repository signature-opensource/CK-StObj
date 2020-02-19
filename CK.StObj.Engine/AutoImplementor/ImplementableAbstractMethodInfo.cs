#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\AutoImplementor\ImplementableAbstractMethodInfo.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Associates an <see cref="IAutoImplementorMethod"/> to use for a <see cref="Method"/>.
    /// </summary>
    public struct ImplementableAbstractMethodInfo
    {
        internal ImplementableAbstractMethodInfo( MethodInfo m, IAutoImplementorMethod impl )
        {
            Method = m;
            ImplementorToUse = impl;
        }

        /// <summary>
        /// Abstract method that has to be automatically implemented.
        /// </summary>
        public readonly MethodInfo Method;

        /// <summary>
        /// The <see cref="IAutoImplementorMethod"/> to use.
        /// </summary>
        public readonly IAutoImplementorMethod ImplementorToUse;

    }

}
