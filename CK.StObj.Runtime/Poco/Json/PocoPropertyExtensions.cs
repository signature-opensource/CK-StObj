using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup.Json
{
    /// <summary>
    /// Provides access to Json information for poco.
    /// </summary>
    public static class PocoPropertyExtensions
    {
        /// <summary>
        /// Gets the <see cref="IPocoJsonInfo"/> associated to this poco.
        /// </summary>
        /// <param name="this">This poco information.</param>
        /// <returns>The Json info if it's available.</returns>
        public static IPocoJsonInfo? GetJsonInfo( this IPocoFamilyInfo @this ) => @this.Annotation<IPocoJsonInfo>();

        /// <summary>
        /// Gets the <see cref="IPocoJsonPropertyInfo"/> associated to this poco property.
        /// </summary>
        /// <param name="this">This poco property.</param>
        /// <returns>The Json info if it's available.</returns>
        public static IPocoJsonPropertyInfo? GetJsonInfo( this IPocoPropertyInfo @this ) => @this.Annotation<IPocoJsonPropertyInfo>();
    }
}
