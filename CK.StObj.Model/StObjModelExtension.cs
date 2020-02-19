#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Model\StObjModelExtension.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Core
{
    /// <summary>
    /// Implements extensions.
    /// </summary>
    static public class StObjModelExtension
    {
        /// <summary>
        /// Gets the structured object or null if no mapping exists.
        /// </summary>
        /// <param name="this">This context.</param>
        /// <typeparam name="T">Type (that must be a Real Object).</typeparam>
        /// <returns>Structured object instance or null if the type has not been mapped.</returns>
        public static T Obtain<T>( this IStObjObjectMap @this )
        {
            return (T)@this.Obtain( typeof( T ) );
        }
    }
}
