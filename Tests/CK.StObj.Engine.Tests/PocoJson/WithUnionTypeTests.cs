using CK.Core;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public class WithUnionTypeTests
    {
        public interface IWithUnions : IPoco
        {
            /// <summary>
            /// Gets or sets a nullable int or string.
            /// </summary>
            [UnionType]
            object? NullableIntOrString { get; set; }

            /// <summary>
            /// Gets or sets a complex algebraic type.
            /// </summary>
            [UnionType]
            object NonNullableListOrDictionaryOrDouble { get; set; }

            [DefaultValue( 3712 )]
            int WithDefaultValue { get; set; }

            struct UnionTypes
            {
                public (int, string)? NullableIntOrString { get; }
                public (List<string?>, Dictionary<IPoco, HashSet<int?>>[], double) NonNullableListOrDictionaryOrDouble { get; }
            }
        }

        [Test]
        public void union_type_serialization()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( IWithUnions ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = s.GetRequiredService<PocoDirectory>();

            var f = s.GetRequiredService<IPocoFactory<IWithUnions>>();
            var oD = f.Create( o => { o.NonNullableListOrDictionaryOrDouble = 58.54; } );
            oD.WithDefaultValue.Should().Be( 3712 );

            var oD2 = JsonTestHelper.Roundtrip( directory, oD );
            oD2.NonNullableListOrDictionaryOrDouble.Should().Be( 58.54 );
            oD2.WithDefaultValue.Should().Be( 3712 );

        }

    }
}
