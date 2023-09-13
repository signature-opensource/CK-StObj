using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests
{
    public static class JsonTestHelper
    {
        public static T? Deserialize<T>( IServiceProvider services, ReadOnlySpan<byte> b, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r, options );
        }

        public static T? Deserialize<T>( IServiceProvider services, string s, PocoJsonSerializerOptions? options = null ) where T : class, IPoco
        {
            return Deserialize<T>( services, Encoding.UTF8.GetBytes( s ), options );
        }

        /// <summary>
        /// Tests a round trip to and from Json with type: the serialized form contains the serialized types.
        /// </summary>
        /// <typeparam name="T">The poco type.</typeparam>
        /// <param name="directory">The Poco directory.</param>
        /// <param name="o">The Poco to serialize/deserialize.</param>
        /// <param name="text">Optional Json text hook called on success.</param>
        /// <returns>The deserialized Poco.</returns>
        [return: NotNullIfNotNull( "o" )]
        public static T? Roundtrip<T>( PocoDirectory directory, T? o, PocoJsonSerializerOptions? options = null, Action<string>? text = null ) where T : class, IPoco
        {
            T? Read( ref Utf8JsonReader r ) => (T?)directory.Read( ref r, options );
            return TestHelper.JsonIdempotenceCheck( o, (w,o) => o.Write( w, true, options ), Read, text );
        }
    }
}
