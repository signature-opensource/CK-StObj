using CK.Poco.Exc.Json.Export;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CK.Core
{
    public static class PocoJsonExportExtensions
    {
        /// <summary>
        /// Serializes this Poco (that can be null) into UTF-8 Json bytes.
        /// </summary>
        /// <param name="this">The poco (can be null).</param>
        /// <param name="withType">True to emit this Poco type.</param>
        /// <param name="options">Optional export options.</param>
        /// <returns>The Utf8 bytes.</returns>
        public static ReadOnlyMemory<byte> JsonSerialize( this IPoco? @this, bool withType = true, PocoJsonExportOptions? options = null )
        {
            var m = new ArrayBufferWriter<byte>();
            using( var w = new Utf8JsonWriter( m ) )
            {
                @this.WriteJson( w, withType, options );
                w.Flush();
            }
            return m.WrittenMemory;
        }
    }
}
