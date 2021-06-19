using CK.CodeGen;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.Json;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [TestFixture]
    public partial class ECMAScriptStandardUnionTypeSerializationTests
    {
        public static readonly PocoJsonSerializerOptions Standard = new PocoJsonSerializerOptions { Mode = PocoJsonSerializerMode.ECMAScriptStandard };

        public interface IOther : IPoco { public int Value { get; set; } }

        [ExternalName("UT")]
        public interface INotCompliant1 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (double, int) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant2 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<int>, int[]) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant3 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<int>, int?[]) Thing { get; }
            }
        }

        [ExternalName("UT")]
        public interface INotCompliant4 : IPoco
        {
            [UnionType]
            public object Thing { get; set; }

            class UnionTypes
            {
                public (List<(int,string)>, (int,string)?[]) Thing { get; }
            }
        }

        [TestCase( typeof( INotCompliant1 ) )]
        [TestCase( typeof( INotCompliant2 ) )]
        [TestCase( typeof( INotCompliant3 ) )]
        [TestCase( typeof( INotCompliant4 ) )]
        public void Non_compliant_Poco_are_detected( Type t )
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), t );
                var services = TestHelper.GetAutomaticServices( c ).Services;
                var directory = services.GetService<PocoDirectory>();

                var u = ((IPocoFactory)services.GetRequiredService( typeof(IPocoFactory<>).MakeGenericType( t ) )).Create();

                FluentActions.Invoking( () => u.JsonSerialize( true, Standard ) ).Should().Throw<NotSupportedException>();
                FluentActions.Invoking( () => u.JsonSerialize( false, Standard ) ).Should().Throw<NotSupportedException>();
                FluentActions.Invoking( () => directory.JsonDeserialize( @"[""UT"",{""Thing"":3}]", Standard ) ).Should().Throw<NotSupportedException>();

                entries.Should().Contain( e => e.Text.Contains( "De/serializing this Poco in 'ECMAScriptstandard' will throw a NotSupportedException.", StringComparison.Ordinal ) );
            }
        }

    }
}
