#region Proprietary License
/*----------------------------------------------------------------------------
* This file (CK.StObj.Engine\AutoImplementor\DynamicAssembly.cs) is part of CK-Database. 
* Copyright © 2007-2014, Invenietis <http://www.invenietis.com>. All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IDynamicAssembly"/>.
    /// </summary>
    public static class DynamicAssemblyExtension
    {
        /// <summary>
        /// Gets a type name in <see cref="IDynamicAssembly.DefaultGenerationNamespace"/>'s namespace
        /// and a <see cref="IDynamicAssembly.NextUniqueNumber"/> suffix or a guid when the <paramref name="name"/> is null.
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <param name="name">Base type name.</param>
        /// <returns>A unique type name.</returns>
        public static string AutoNextTypeName(this IDynamicAssembly @this, string name = null)
        {
            return @this.DefaultGenerationNamespace.FullName
                    + '.'
                    + (name != null
                            ? name + @this.NextUniqueNumber()
                            : "G" + Guid.NewGuid().ToString( "N" ));
        }

        /// <summary>
        /// Gets all information related to Poco.
        /// </summary>
        /// <param name="this">This Dynamic assembly.</param>
        /// <returns>The Poco information.</returns>
        public static IPocoSupportResult GetPocoInfo( this IDynamicAssembly @this ) => (IPocoSupportResult)@this.Memory[typeof( IPocoSupportResult )];

    }
}
