using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;
using System.ComponentModel;
using System.Diagnostics;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class PocoJsonTests
    {
        public interface ITest : IPoco
        {
            int Power { get; set; }

            [DefaultValue( "Hello..." )]
            string Hip { get; set; }
        }

        [Test]
        public void null_poco_is_handled()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            ITest? nullPoco = null;

            ITest? o2 = Roundtrip( s, nullPoco );
            o2.Should().BeNull();

            IPoco? nullUnknwonPoco = null;

            IPoco? o3 = Roundtrip( s, nullUnknwonPoco );
            o3.Should().BeNull();
        }

        [Test]
        public void simple_poco_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ITest ) ); ;
            var s = TestHelper.GetAutomaticServices( c ).Services;

            var f = s.GetRequiredService<IPocoFactory<ITest>>();
            var o = f.Create( o => { o.Power = 3712; o.Hip += "CodeGen!"; } );
            var o2 = Roundtrip( s, o );

            Debug.Assert( o2 != null );
            o2.Power.Should().Be( o.Power );
            o2.Hip.Should().Be( o.Hip );
        }

        static byte[] Serialize( IPoco o )
        {
            var m = new MemoryStream();
            using( var w = new Utf8JsonWriter( m ) )
            {
                w.WritePocoValue( o );
                w.Flush();
            }
            return m.ToArray();
        }

        static T? Deserialize<T>( IServiceProvider services, byte[] b ) where T : class, IPoco
        {
            var r = new Utf8JsonReader( b );
            var f = services.GetRequiredService<IPocoFactory<T>>();
            return f.Read( ref r );
        }

        static T? Roundtrip<T>( IServiceProvider services, T? o ) where T : class, IPoco
        {
            var directory = services.GetService<PocoDirectory>();
            using( var m = new MemoryStream() )
            {
                using( var w = new Utf8JsonWriter( m ) )
                {
                    w.WritePocoValue( o );
                    w.Flush();
                }
                var bin1 = m.ToArray();
                string textForDebug = Encoding.UTF8.GetString( bin1 );

                var r1 = new Utf8JsonReader( bin1 );

                var o2 = directory.ReadPocoValue( ref r1 );

                m.Position = 0;
                using( var w2 = new Utf8JsonWriter( m ) )
                {
                    w2.WritePocoValue( o2 );
                    w2.Flush();
                }
                var bin2 = m.ToArray();

                bin1.Should().BeEquivalentTo( bin2 );

                // Is this an actual Poco or a definer?
                // When it's a definer, there is no factory!
                var f = services.GetService<IPocoFactory<T>>();
                if( f != null )
                {
                    var r2 = new Utf8JsonReader( bin2 );
                    var o3 = f.Read( ref r2 );
                    return o3;
                }
                return (T?)o2;
            }

        }

    }
}
