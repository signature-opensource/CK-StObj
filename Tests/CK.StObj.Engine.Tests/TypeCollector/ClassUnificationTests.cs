using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

namespace CK.StObj.Engine.Tests.Service.TypeCollector
{
    [TestFixture]
    public class ClassUnificationTests : TypeCollectorTestsBase
    {
        public class A : IScopedAutoService { }
        public class AS1 : A { }
        public class AS2 : A { }
        public class UnifiedA : A { public UnifiedA( AS1 a1, AS2 a2 ) { } }
        public class UnifiedAWithoutS2 : A { public UnifiedAWithoutS2( AS1 a1 ) { } }

        [Test]
        public void basic_direct_unification_between_3_specializations()
        {
            var r = CheckSuccess( collector =>
            {
                collector.RegisterClass( TestHelper.Monitor, typeof( UnifiedA ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( AS1 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( AS2 ) );
            } );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            var c = r.AutoServices.RootClasses[0].MostSpecialized;
            Debug.Assert( c != null );
            c.ClassType.Should().BeSameAs( typeof( UnifiedA ) );
        }

        [Test]
        public void basic_direct_unification_between_2_specializations()
        {
            var r = CheckSuccess( collector =>
            {
                collector.RegisterClass( TestHelper.Monitor, typeof( UnifiedAWithoutS2 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( AS1 ) );
            } );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            r.AutoServices.RootClasses[0].MostSpecialized!.ClassType.Should().BeSameAs( typeof( UnifiedAWithoutS2 ) );
        }

        public class _A : IScopedAutoService { }
        public class _AS1 : _A { }
        public class _AS2 : _A { }
        public class _AS3 : _A { }
        public class _UnifiedA1 : _A { public _UnifiedA1( _AS1 a1, _AS2 a2 ) { } }
        public class _UnifiedA2 : _A { public _UnifiedA2( _UnifiedA1 u, _AS3 a3 ) { } }

        [Test]
        public void unification_with_intermediate_unifier()
        {
            var r = CheckSuccess( collector =>
            {
                collector.RegisterClass( TestHelper.Monitor, typeof( _UnifiedA1 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( _UnifiedA2 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( _AS1 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( _AS2 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( _AS3 ) );
            } );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            r.AutoServices.RootClasses[0].MostSpecialized!.ClassType.Should().BeSameAs( typeof( _UnifiedA2 ) );
        }

        public class e_A : IScopedAutoService { }
        public class e_AS1 : e_A { }
        public class e_AS2 : e_A { }
        public class e_AS3 : e_A { }
        public class ExternalUnifier : IScopedAutoService { public ExternalUnifier( e_AS1 a1, e_AS2 a2 ) { } }
        public class e_UnifiedA2 : e_A { public e_UnifiedA2( ExternalUnifier u, e_AS3 a3 ) { } }

        [Test]
        public void unification_with_intermediate_external_unifier()
        {
            var r = CheckSuccess( collector =>
            {
                collector.RegisterClass( TestHelper.Monitor, typeof( ExternalUnifier ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( e_UnifiedA2 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( e_AS1 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( e_AS2 ) );
                collector.RegisterClass( TestHelper.Monitor, typeof( e_AS3 ) );
            } );
            r.AutoServices.RootClasses.Should().HaveCount( 2 );
            r.AutoServices.RootClasses.Single( c => c.ClassType == typeof( e_A ) ).MostSpecialized!.ClassType
                .Should().BeSameAs( typeof( e_UnifiedA2 ) );
        }


        public class u_A : IScopedAutoService { }
        public class u_AS1 : u_A { }
        public class u_AS2Base : u_A { }
        public class u_AS2 : u_AS2Base { }
        public class u_UnifiedD : u_A { public u_UnifiedD( u_AS1 a1, u_AS2Base a2 ) { } }
        public class u_UnifiedA : u_A { public u_UnifiedA( u_AS1 a1, u_AS2 a2 ) { } }

        [Test]
        public void unification_to_base_class()
        {
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_AS1 ) );
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_AS2 ) );
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_UnifiedD ) );
                } );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                r.AutoServices.RootClasses.Single( c => c.ClassType == typeof( u_A ) ).MostSpecialized!.ClassType
                    .Should().BeSameAs( typeof( u_UnifiedD ) );
            }
            {
                var r = CheckSuccess( collector =>
                {
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_AS1 ) );
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_AS2 ) );
                    collector.RegisterClass( TestHelper.Monitor, typeof( u_UnifiedA ) );
                } );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                r.AutoServices.RootClasses.Single( c => c.ClassType == typeof( u_A ) ).MostSpecialized!.ClassType
                    .Should().BeSameAs( typeof( u_UnifiedA ) );
            }
        }

        [Test]
        public void unification_failure_on_two_potential_unifiers()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( TestHelper.Monitor, typeof( u_AS1 ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( u_AS2 ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( u_UnifiedD ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( u_UnifiedA ) );
            var r = CheckFailure( collector );
        }

        public class s_A : IScopedAutoService { }
        public class s_AS1 : s_A { }
        public class s_AS2Base : s_A { }
        public class s_AS2aBase : s_AS2Base { }
        public class s_AS2a : s_AS2aBase { }
        public class s_AS2b : s_AS2Base { }

        // Supergraph unifiers:
        public class s_UnifiedAD : s_A { public s_UnifiedAD( s_AS1 a1, s_AS2Base a2 ) { } }
        public class s_UnifiedAaBase : s_A { public s_UnifiedAaBase( s_AS1 a1, s_AS2aBase a2 ) { } }
        public class s_UnifiedAa : s_A { public s_UnifiedAa( s_AS1 a1, s_AS2a a2 ) { } }
        public class s_UnifiedAb : s_A { public s_UnifiedAb( s_AS1 a1, s_AS2b a2 ) { } }

        // Subgraph unifiers:
        public class s_SubUnifier1 : s_AS2Base { public s_SubUnifier1( s_AS2a a, s_AS2b b ) { } }
        public class s_SubUnifier2 : s_AS2Base { public s_SubUnifier2( s_AS2aBase a, s_AS2b b ) { } }
        public class s_SubUnifierBase3 : s_AS2Base { }
        public class s_SubUnifier3 : s_SubUnifierBase3 { public s_SubUnifier3( s_AS2aBase a, s_AS2b b ) { } }

        [TestCase( typeof( s_UnifiedAD ) )]
        [TestCase( typeof( s_UnifiedAaBase ) )]
        [TestCase( typeof( s_UnifiedAa ) )]
        [TestCase( typeof( s_UnifiedAb ) )]
        public void subgraph_requires_unification( Type unifier )
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS1 ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2a ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2b ) );
            collector.RegisterClass( TestHelper.Monitor, unifier );
            var r = CheckFailure( collector );
            var ambiguities = r.AutoServices.ClassAmbiguities;
            ambiguities.Should().HaveCount( 1 );
            var a = ambiguities[0];
            a.Should().HaveCount( 1 + 2 );
            a[0].ClassType.Should().BeSameAs( typeof( s_AS2Base ) );
            a.Skip( 1 ).Select( i => i.ClassType ).Should().BeEquivalentTo( new[] { typeof( s_AS2a ), typeof( s_AS2b ) } );
        }

        [TestCase( typeof( s_SubUnifier1 ) )]
        [TestCase( typeof( s_SubUnifier2 ) )]
        [TestCase( typeof( s_SubUnifier3 ) )]
        public void supergraph_requires_unification( Type unifier )
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS1 ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2a ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2b ) );
            collector.RegisterClass( TestHelper.Monitor, unifier );
            var r = CheckFailure( collector );
            var ambiguities = r.AutoServices.ClassAmbiguities;
            ambiguities.Should().HaveCount( 1 );
            var a = ambiguities[0];
            a.Should().HaveCount( 1 + 4 );
            a[0].ClassType.Should().BeSameAs( typeof( s_A ) );
            a.Skip( 1 ).Select( i => i.ClassType ).Should().BeEquivalentTo( new[] { typeof( s_AS1 ), unifier, typeof( s_AS2a ), typeof( s_AS2b ) } );
        }

        [Test]
        public void graph_with_two_ambiguities()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS1 ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2a ) );
            collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2b ) );
            var r = CheckFailure( collector );
            var ambiguities = r.AutoServices.ClassAmbiguities;
            ambiguities.Should().HaveCount( 2 );
        }

        [Test]
        public void graph_with_two_unifications()
        {
            foreach( var super in new[] { typeof( s_UnifiedAD ), typeof( s_UnifiedAaBase ), typeof( s_UnifiedAa ), typeof( s_UnifiedAb ) } )
            {
                foreach( var sub in new[] { typeof( s_SubUnifier1 ), typeof( s_SubUnifier2 ), typeof( s_SubUnifier3 ) } )
                {
                    CheckSuccess( collector =>
                    {
                        collector.RegisterClass( TestHelper.Monitor, typeof( s_AS1 ) );
                        collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2a ) );
                        collector.RegisterClass( TestHelper.Monitor, typeof( s_AS2b ) );
                        collector.RegisterClass( TestHelper.Monitor, super );
                        collector.RegisterClass( TestHelper.Monitor, sub );
                    } );
                }
            }
        }

    }
}
