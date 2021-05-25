//using CK.Core;
//using FluentAssertions;
//using Microsoft.Extensions.DependencyInjection;
//using NUnit.Framework;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using static CK.Testing.StObjEngineTestHelper;

//namespace CK.StObj.Engine.Tests.Poco
//{
//    [TestFixture]
//    public class PocoPropertyAssignableTests
//    {
//        public interface IAlways : IPoco
//        {
//            object? Thing { get; set; }
//        }

//        public interface INullableString : IPoco
//        {
//            string? Thing { get; set; }
//        }

//        public interface INonNullableString : IPoco
//        {
//            string? Thing { get; set; }
//        }

//        public interface IPocoOrString : IPoco
//        {
//            [UnionType(typeof(string),typeof(IPoco))]
//            object Thing { get; set; }
//        }


//        public interface IPocoIntOrString : IPoco
//        {
//            [UnionType( typeof( (IPoco, int, string) ) )]
//            object Thing { get; set; }
//        }

//        public interface INullablePocoIntOrString : IPoco
//        {
//            [UnionType( typeof( (string, int) ) )]
//            object Thing { get; set; }
//        }

//        [Test]
//        public void Union_property_guard_the_allowed_types_and_null_is_allowed_if_one_of_the_variant_is_nullable()
//        {
//            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithUnionType ) );
//            var s = TestHelper.GetAutomaticServices( c ).Services;
//            var p = s.GetRequiredService<IPocoFactory<IPocoWithUnionType>>().Create();

//            p.Thing = 34;
//            p.Thing.Should().Be( 34 );
//            p.Thing = "lklk";
//            p.Thing.Should().Be( "lklk" );
//            p.Thing = null!;
//            p.Thing.Should().BeNull();

//            p.Invoking( x => x.Thing = 25.88 ).Should().Throw<ArgumentException>();
//            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();
//        }

//        public interface IPocoWithUnionTypeNotEventuallyNullable : IPoco
//        {
//            [UnionType( typeof( decimal ), typeof( int ) )]
//            object Thing { get; set; }
//        }

//        [Test]
//        public void Union_property_guard_the_allowed_types_and_null_is_NOT_allowed_if_none_of_the_variant_is_nullable()
//        {
//            var c = TestHelper.CreateStObjCollector( typeof( IPocoWithUnionTypeNotEventuallyNullable ) );
//            var s = TestHelper.GetAutomaticServices( c ).Services;
//            var p = s.GetRequiredService<IPocoFactory<IPocoWithUnionTypeNotEventuallyNullable>>().Create();

//            p.Thing = 34;
//            p.Thing.Should().Be( 34 );
//            p.Thing = (decimal)555;
//            p.Thing.Should().Be( (decimal)555 );

//            p.Invoking( x => x.Thing = null! ).Should().Throw<ArgumentException>();
//            p.Invoking( x => x.Thing = this ).Should().Throw<ArgumentException>();
//        }


//    }
//}
