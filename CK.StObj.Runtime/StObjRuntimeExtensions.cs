#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Runtime\CustomAttributeProviderComposite.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion


namespace CK.Setup
{
    /// <summary>
    /// Provides useful extensions.
    /// </summary>
    public static class StObjRuntimeExtensions
    {
        /// <summary>
        /// Gets whether <see cref="IStObjResult.GetStObjProperty"/> returns an object that is not the
        /// special <see cref="System.Type.Missing"/> marker object.
        /// </summary>
        /// <param name="this">This StObj.</param>
        /// <param name="propertyName">Name of the property. Must not be null nor empty.</param>
        /// <returns>True if the property is defined, false otherwise.</returns>
        public static bool HasStObjProperty( this IStObjResult @this, string propertyName ) => @this.GetStObjProperty( propertyName ) != System.Type.Missing;
    }


}
