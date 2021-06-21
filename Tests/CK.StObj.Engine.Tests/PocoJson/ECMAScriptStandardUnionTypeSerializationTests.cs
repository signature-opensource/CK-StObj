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
                public (float, int) Thing { get; }
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

        [ExternalName( "UT" )]
        public interface ICompliant1 : IPoco
        {
            [UnionType]
            public object T1 { get; set; }

            [UnionType]
            public object? T2 { get; set; }

            [UnionType]
            public object T3 { get; set; }

            class UnionTypes
            {
                public (double, int) T1 { get; }
                public (int, double?) T2 { get; }
                public (byte,sbyte,double,float) T3 { get; }
            }
        }

        [Test]
        public void number_types_with_a_double_among_them_are_compliant()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ICompliant1 ) );
            var services = TestHelper.GetAutomaticServices( c ).Services;
            var directory = services.GetService<PocoDirectory>();

            var u = services.GetRequiredService<IPocoFactory<ICompliant1>>().Create();
            u.T1 = 1;
            u.T2 = 2;
            u.T3 = (byte)3;

            var serialized = u.JsonSerialize( true, Standard );
            Encoding.UTF8.GetString( serialized.Span ).Should().Be( @"{""UT"":{""T1"":1,""T2"":2,""T3"":3}}" );

            directory.JsonDeserialize( serialized.Span );
        }


        [ExternalName( "UT" )]
        public interface ICompliant2 : IPoco
        {
            [UnionType]
            public object T1 { get; set; }

            [UnionType]
            public object T2 { get; set; }

            class UnionTypes
            {
                public (IList<double>, double[], ISet<double>) T1 { get; }

                public (IDictionary<string,double>, IDictionary<string, byte>) T2 { get; }
            }
        }



    }
}
