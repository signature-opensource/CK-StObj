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
        public static readonly PocoJsonSerializerOptions ECMAScriptStandard = new PocoJsonSerializerOptions { Mode = PocoJsonSerializerMode.ECMAScriptStandard };

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
        [TestCase( typeof( INotCompliant3 ) )]
        [TestCase( typeof( INotCompliant4 ) )]
        public void Non_compliant_Poco_are_detected( Type t )
        {
            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Warn ) )
            {
                var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), t );
                using var services = TestHelper.CreateAutomaticServices( c ).Services;
                var directory = services.GetRequiredService<PocoDirectory>();

                var u = ((IPocoFactory)services.GetRequiredService( typeof(IPocoFactory<>).MakeGenericType( t ) )).Create();

                FluentActions.Invoking( () => u.JsonSerialize( true, ECMAScriptStandard ) ).Should().Throw<NotSupportedException>();
                FluentActions.Invoking( () => u.JsonSerialize( false, ECMAScriptStandard ) ).Should().Throw<NotSupportedException>();
                FluentActions.Invoking( () => directory.JsonDeserialize( @"[""UT"",{""Thing"":3}]", ECMAScriptStandard ) ).Should().Throw<NotSupportedException>();

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
                public (int, double)? T2 { get; }
                public (byte,sbyte,double,float) T3 { get; }
            }
        }

        [Test]
        public void number_types_with_a_double_among_them_are_compliant()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ICompliant1 ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = services.GetRequiredService<PocoDirectory>();

            var u = services.GetRequiredService<IPocoFactory<ICompliant1>>().Create();
            u.T1 = 1;
            u.T2 = 2;
            u.T3 = (byte)3;

            var serialized = u.JsonSerialize( true, ECMAScriptStandard );
            Encoding.UTF8.GetString( serialized.Span ).Should().Be( @"[""UT"",{""T1"":[""Number"",1],""T2"":[""Number"",2],""T3"":[""Number"",3]}]" );

            var r = (ICompliant1?)directory.JsonDeserialize( serialized.Span, ECMAScriptStandard );
            // This consider T3 (double) = 3.0 to be equivalent to (byte)3!
            // r.Should().BeEquivalentTo( u );
            Debug.Assert( r != null );
            r.T1.Should().BeOfType<double>().And.Be( u.T1 );
            r.T2.Should().BeOfType<double>().And.Be( u.T2 );
            r.T3.Should().BeOfType<double>().And.Be( u.T3 );
        }

        [ExternalName( "UT" )]
        public interface ICompliant2 : IPoco
        {
            [UnionType]
            public object T1 { get; set; }

            [UnionType]
            public object? T2 { get; set; }

            class UnionTypes
            {
                public (int, string) T1 { get; }
                public (byte, long)? T2 { get; }
            }
        }

        [Test]
        public void when_a_number_is_not_ambiguous_it_is_correctly_typed()
        {
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ICompliant2 ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = services.GetRequiredService<PocoDirectory>();

            var u = services.GetRequiredService<IPocoFactory<ICompliant2>>().Create();
            u.T1 = 1;
            u.T2 = (byte)2;

            var serialized = u.JsonSerialize( true, ECMAScriptStandard );
            Encoding.UTF8.GetString( serialized.Span ).Should().Be( @"[""UT"",{""T1"":[""Number"",1],""T2"":[""Number"",2]}]" );

            var r = (ICompliant2?)directory.JsonDeserialize( serialized.Span, ECMAScriptStandard );
            Debug.Assert( r != null );
            r.T1.Should().BeOfType<int>().And.Be( u.T1 );
            r.T2.Should().BeOfType<byte>().And.Be( u.T2 );
        }


        [ExternalName( "UT" )]
        public interface ICompliant3 : IPoco
        {
            [UnionType]
            public object T1 { get; set; }

            [UnionType]
            public object T2 { get; set; }

            class UnionTypes
            {
                public (List<double>, HashSet<double>) T1 { get; }

                public (Dictionary<string,double>, Dictionary<string, byte>) T2 { get; }
            }
        }


        [Test]
        public void types_can_mute_across_serialization()
        {
            // across 
            var c = TestHelper.CreateStObjCollector( typeof( PocoJsonSerializer ), typeof( ICompliant3 ) );
            using var services = TestHelper.CreateAutomaticServices( c ).Services;
            var directory = services.GetRequiredService<PocoDirectory>();

            var u = services.GetRequiredService<IPocoFactory<ICompliant3>>().Create();
            u.T1 = new List<double> { 2.5d, 85.8d };
            u.T2 = new Dictionary<string, byte> { { "A", 1 }, { "B", 2 } };

            var serialized = u.JsonSerialize( true, ECMAScriptStandard );
            Encoding.UTF8.GetString( serialized.Span ).Should().Be( @"[""UT"",{""T1"":[""Number[]"",[2.5,85.8]],""T2"":[""O(Number)"",{""A"":1,""B"":2}]}]" );

            var r = (ICompliant3?)directory.JsonDeserialize( serialized.Span, ECMAScriptStandard );
            Debug.Assert( r != null );
            r.T1.Should().BeOfType<List<double>>().And.BeEquivalentTo( u.T1 );
            r.T2.Should().BeOfType<Dictionary<string, double>>().And.BeEquivalentTo( u.T2 );

            u.T1 = new HashSet<double>() { 2.5d, 85.8d };

            serialized = u.JsonSerialize( true, ECMAScriptStandard );
            Encoding.UTF8.GetString( serialized.Span ).Should().Be( @"[""UT"",{""T1"":[""S(Number)"",[2.5,85.8]],""T2"":[""O(Number)"",{""A"":1,""B"":2}]}]" );

            r = (ICompliant3?)directory.JsonDeserialize( serialized.Span, ECMAScriptStandard );
            Debug.Assert( r != null );
            r.T1.Should().BeOfType<HashSet<double>>().And.BeEquivalentTo( u.T1 );
            r.T2.Should().BeOfType<Dictionary<string, double>>().And.BeEquivalentTo( u.T2 );


        }


    }
}
