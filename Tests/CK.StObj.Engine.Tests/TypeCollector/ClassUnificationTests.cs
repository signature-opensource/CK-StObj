using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;

namespace CK.StObj.Engine.Tests.Service.TypeCollector
{
    [TestFixture]
    public class ClassUnificationTests : TypeCollectorTestsBase
    {
        class A : IScopedAutoService { }
        class AS1 : A { }
        class AS2 : A { }
        class UnifiedA : A { public UnifiedA( AS1 a1, AS2 a2 ) { } }
        class UnifiedAWithoutS2 : A { public UnifiedAWithoutS2( AS1 a1 ) { } }

        [Test]
        public void basic_direct_unification_between_3_specializations()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( UnifiedA ) );
            collector.RegisterClass( typeof( AS1 ) );
            collector.RegisterClass( typeof( AS2 ) );
            var r = CheckSuccess( collector );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            r.AutoServices.RootClasses[0].MostSpecialized.Type.Should().BeSameAs( typeof( UnifiedA ) );
        }

        [Test]
        public void basic_direct_unification_between_2_specializations()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( UnifiedAWithoutS2 ) );
            collector.RegisterClass( typeof( AS1 ) );
            var r = CheckSuccess( collector );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            r.AutoServices.RootClasses[0].MostSpecialized.Type.Should().BeSameAs( typeof( UnifiedAWithoutS2 ) );
        }

        class _A : IScopedAutoService { }
        class _AS1 : _A { }
        class _AS2 : _A { }
        class _AS3 : _A { }
        class _UnifiedA1 : _A { public _UnifiedA1( _AS1 a1, _AS2 a2 ) { } }
        class _UnifiedA2 : _A { public _UnifiedA2( _UnifiedA1 u, _AS3 a3 ) { } }

        [Test]
        public void unification_with_intermediate_unifier()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( _UnifiedA1 ) );
            collector.RegisterClass( typeof( _UnifiedA2 ) );
            collector.RegisterClass( typeof( _AS1 ) );
            collector.RegisterClass( typeof( _AS2 ) );
            collector.RegisterClass( typeof( _AS3 ) );
            var r = CheckSuccess( collector );
            r.AutoServices.RootClasses.Should().HaveCount( 1 );
            r.AutoServices.RootClasses[0].MostSpecialized.Type.Should().BeSameAs( typeof( _UnifiedA2 ) );
        }

        class e_A : IScopedAutoService { }
        class e_AS1 : e_A { }
        class e_AS2 : e_A { }
        class e_AS3 : e_A { }
        class ExternalUnifier : IScopedAutoService { public ExternalUnifier( e_AS1 a1, e_AS2 a2 ) { } }
        class e_UnifiedA2 : e_A { public e_UnifiedA2( ExternalUnifier u, e_AS3 a3 ) { } }

        [Test]
        public void unification_with_intermediate_external_unifier()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( ExternalUnifier ) );
            collector.RegisterClass( typeof( e_UnifiedA2 ) );
            collector.RegisterClass( typeof( e_AS1 ) );
            collector.RegisterClass( typeof( e_AS2 ) );
            collector.RegisterClass( typeof( e_AS3 ) );
            var r = CheckSuccess( collector );
            r.AutoServices.RootClasses.Should().HaveCount( 2 );
            r.AutoServices.RootClasses.Single( c => c.Type == typeof( e_A ) ).MostSpecialized.Type
                .Should().BeSameAs( typeof( e_UnifiedA2 ) );
        }


        class u_A : IScopedAutoService { }
        class u_AS1 : u_A { }
        class u_AS2Base : u_A { }
        class u_AS2 : u_AS2Base { }
        class u_UnifiedD : u_A { public u_UnifiedD( u_AS1 a1, u_AS2Base a2 ) { } }
        class u_UnifiedA : u_A { public u_UnifiedA( u_AS1 a1, u_AS2 a2 ) { } }

        [Test]
        public void unification_to_base_class()
        {
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterClass( typeof( u_AS1 ) );
                collector.RegisterClass( typeof( u_AS2 ) );
                collector.RegisterClass( typeof( u_UnifiedD ) );
                var r = CheckSuccess( collector );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                r.AutoServices.RootClasses.Single( c => c.Type == typeof( u_A ) ).MostSpecialized.Type
                    .Should().BeSameAs( typeof( u_UnifiedD ) );
            }
            {
                var collector = CreateCKTypeCollector();
                collector.RegisterClass( typeof( u_AS1 ) );
                collector.RegisterClass( typeof( u_AS2 ) );
                collector.RegisterClass( typeof( u_UnifiedA ) );
                var r = CheckSuccess( collector );
                r.AutoServices.RootClasses.Should().HaveCount( 1 );
                r.AutoServices.RootClasses.Single( c => c.Type == typeof( u_A ) ).MostSpecialized.Type
                    .Should().BeSameAs( typeof( u_UnifiedA ) );
            }
        }

        [Test]
        public void unification_failure_on_two_potential_unifiers()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( u_AS1 ) );
            collector.RegisterClass( typeof( u_AS2 ) );
            collector.RegisterClass( typeof( u_UnifiedD ) );
            collector.RegisterClass( typeof( u_UnifiedA ) );
            var r = CheckFailure( collector );
        }

        class s_A : IScopedAutoService { }
        class s_AS1 : s_A { }
        class s_AS2Base : s_A { }
        class s_AS2aBase : s_AS2Base { }
        class s_AS2a : s_AS2aBase { }
        class s_AS2b : s_AS2Base { }

        // Supergraph unifiers:
        class s_UnifiedAD : s_A { public s_UnifiedAD( s_AS1 a1, s_AS2Base a2 ) { } }
        class s_UnifiedAaBase : s_A { public s_UnifiedAaBase( s_AS1 a1, s_AS2aBase a2 ) { } }
        class s_UnifiedAa : s_A { public s_UnifiedAa( s_AS1 a1, s_AS2a a2 ) { } }
        class s_UnifiedAb : s_A { public s_UnifiedAb( s_AS1 a1, s_AS2b a2 ) { } }

        // Subgraph unifiers:
        class s_SubUnifier1 : s_AS2Base { public s_SubUnifier1( s_AS2a a, s_AS2b b ) { } }
        class s_SubUnifier2 : s_AS2Base { public s_SubUnifier2( s_AS2aBase a, s_AS2b b ) { } }
        class s_SubUnifierBase3 : s_AS2Base { }
        class s_SubUnifier3 : s_SubUnifierBase3 { public s_SubUnifier3( s_AS2aBase a, s_AS2b b ) { } }

        [TestCase( typeof( s_UnifiedAD ) )]
        [TestCase( typeof( s_UnifiedAaBase ) )]
        [TestCase( typeof( s_UnifiedAa ) )]
        [TestCase( typeof( s_UnifiedAb ) )]
        public void subgraph_requires_unification( Type unifier )
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( s_AS1 ) );
            collector.RegisterClass( typeof( s_AS2a ) );
            collector.RegisterClass( typeof( s_AS2b ) );
            collector.RegisterClass( unifier );
            var r = CheckFailure( collector );
            var ambiguities = r.AutoServices.ClassAmbiguities;
            ambiguities.Should().HaveCount( 1 );
            var a = ambiguities[0];
            a.Should().HaveCount( 1 + 2 );
            a[0].Type.Should().BeSameAs( typeof( s_AS2Base ) );
            a.Skip( 1 ).Select( i => i.Type ).Should().BeEquivalentTo( typeof( s_AS2a ), typeof( s_AS2b ) );
        }

        [TestCase( typeof( s_SubUnifier1 ) )]
        [TestCase( typeof( s_SubUnifier2 ) )]
        [TestCase( typeof( s_SubUnifier3 ) )]
        public void supergraph_requires_unification( Type unifier )
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( s_AS1 ) );
            collector.RegisterClass( typeof( s_AS2a ) );
            collector.RegisterClass( typeof( s_AS2b ) );
            collector.RegisterClass( unifier );
            var r = CheckFailure( collector );
            var ambiguities = r.AutoServices.ClassAmbiguities;
            ambiguities.Should().HaveCount( 1 );
            var a = ambiguities[0];
            a.Should().HaveCount( 1 + 4 );
            a[0].Type.Should().BeSameAs( typeof( s_A ) );
            a.Skip( 1 ).Select( i => i.Type ).Should().BeEquivalentTo( typeof( s_AS1 ), unifier, typeof( s_AS2a ), typeof( s_AS2b ) );
        }

        [Test]
        public void graph_with_two_ambiguities()
        {
            var collector = CreateCKTypeCollector();
            collector.RegisterClass( typeof( s_AS1 ) );
            collector.RegisterClass( typeof( s_AS2a ) );
            collector.RegisterClass( typeof( s_AS2b ) );
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
                    var collector = CreateCKTypeCollector();
                    collector.RegisterClass( typeof( s_AS1 ) );
                    collector.RegisterClass( typeof( s_AS2a ) );
                    collector.RegisterClass( typeof( s_AS2b ) );
                    collector.RegisterClass( super );
                    collector.RegisterClass( sub );
                    CheckSuccess( collector );
                }
            }
        }

    }
}
