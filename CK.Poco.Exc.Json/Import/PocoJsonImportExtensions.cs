using CK.Poco.Exc.Json.Export;
using CK.Poco.Exc.Json.Import;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Core
{
    public static class PocoJsonImportExtensions
    {
        /// <summary>
        /// Reads a typed Poco (that can be null) from Utf8 encoded bytes.
        /// <para>
        /// If the buffer starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the buffer starts with a '{', then it must be the Poco's value.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( utf8Json );
            return @this.ReadJson( ref r, options );
        }

        /// <summary>
        /// Reads a typed Poco (that can be null) from a string.
        /// <para>
        /// If the buffer starts with a '[', it must be a 2-cells array with this Poco's type that
        /// comes first (otherwise an exception is thrown).
        /// If the buffer starts with a '{', then it must be the Poco's value.
        /// </para>
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="s">The string to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static T? JsonDeserialize<T>( this IPocoFactory<T> @this, string s, PocoJsonImportOptions? options = null ) where T : class, IPoco
        {
            return JsonDeserialize( @this, Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }

        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from Utf8 encoded bytes.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="utf8Json">The utf8 encoded bytes to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, ReadOnlySpan<byte> utf8Json, PocoJsonImportOptions? options = null )
        {
            var r = new Utf8JsonReader( utf8Json );
            return @this.ReadJson( ref r, options );
        }


        /// <summary>
        /// Reads a <see cref="IPoco"/> (that can be null) from a JSON string.
        /// The Poco must have been written with its type.
        /// </summary>
        /// <param name="this">This directory.</param>
        /// <param name="s">The string to deserialize.</param>
        /// <param name="options">Optional import options.</param>
        /// <returns>The Poco (can be null).</returns>
        public static IPoco? JsonDeserialize( this PocoDirectory @this, string s, PocoJsonImportOptions? options = null )
        {
            return JsonDeserialize( @this, Encoding.UTF8.GetBytes( s ).AsSpan(), options );
        }
    }
}
