using CK.Core;
using CK.Setup;
using CK.StObj.Engine.Tests.Poco.Sample;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests.Poco
{
    [TestFixture]
    public class PocoTests
    {
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class PackageWithBasicPoco : IRealObject
        {
            void StObjConstruct( IPocoFactory<IBasicPoco> f )
            {
                Factory = f;
            }

            public IPocoFactory<IBasicPoco> Factory { get; private set; }

        }

        [Test]
        public void simple_poco_resolution_and_injection()
        {
            StObjCollectorResult result = BuildPocoSample( typeof( PackageWithBasicPoco ) );
            Debug.Assert( result.EngineMap != null );

            IStObjResult p = result.EngineMap.StObjs.ToHead( typeof( PackageWithBasicPoco ) )!;
            var package = (PackageWithBasicPoco)p.FinalImplementation.Implementation;
            package.Factory.Should().NotBeNull();
        }

        static StObjCollectorResult BuildPocoSample( params Type[] extra )
        {
            var types = typeof( PocoTests ).Assembly.GetTypes()
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.Poco.Sample" )
                            .Concat( extra );

            StObjCollector c = TestHelper.CreateStObjCollector( types.ToArray() );
            return TestHelper.GetSuccessfulResult( c );
        }

        [CKTypeDefiner]
        public interface IThingBase : IPoco
        {
            int BaseId { get; set; }
        }

        public interface IThing : IThingBase
        {
            int SpecId { get; set; }
        }

        [Test]
        public void poco_marked_with_CKTypeDefiner_are_not_registered()
        {
            StObjCollector collector = TestHelper.CreateStObjCollector( typeof( IThing ) );
            collector.FatalOrErrors.Count.Should().Be( 0 );
            var poco = collector.GetResult( TestHelper.Monitor ).CKTypeResult.PocoDirectory;
            Debug.Assert( poco != null, "Since there has been no error." );
            poco.Families.Should().HaveCount( 1 );

            var TF = poco.Families[0].PocoFactoryClass;
            var F = Activator.CreateInstance( TF );
            Debug.Assert( F != null );
            var FP = (IPocoFactory)F;
            var PT = FP.PocoClassType;
            PT.Should().NotBeNull();

            poco.AllInterfaces.Should().HaveCount( 1 );
            poco.Find( typeof( IThingBase ) ).Should().BeNull();
            poco.Find( typeof( IThing ) ).Should().NotBeNull();
        }

        [Test]
        public void Engine_poco_factory_exposes_the_final_type_but_not_other_properties()
        {
            StObjCollectorResult result = BuildPocoSample();
            Debug.Assert( result.EngineMap != null, "No error." );

            var p = result.EngineMap.StObjs.Obtain<IPocoFactory<IBasicPoco>>()!;

            Type pocoType = p.PocoClassType;
            Assert.That( typeof( IBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEAlternateBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IECombineBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEIndependentBasicPoco ).IsAssignableFrom( pocoType ) );
            // These are dumb emit implementations.
            p.PrimaryInterface.Should().BeNull();
            p.Interfaces.Should().BeNull();
            p.Name.Should().BeNull();
            p.PreviousNames.Should().BeNull();
            p.ClosureInterface.Should().BeNull();
        }

        public interface IDefTest : IPoco
        {
            [DefaultValue( 3712 )]
            int PDef { get; set; }

            [DefaultValue( "Hello \"World\"!" )]
            string Message { get; set; }
        }

        [Test]
        public void poco_property_supports_DefaultValueAttribute_from_System_ComponentModel()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IDefTest ) );
            using var s = TestHelper.CreateAutomaticServices( c ).Services;
            var f = s.GetRequiredService<IPocoFactory<IDefTest>>();
            var o = f.Create();
            o.PDef.Should().Be( 3712 );
            o.Message.Should().Be( @"Hello ""World""!" );
        }

        public interface IDefPropInt : IDefBase
        {
            int PDef { get; set; }
        }

        public interface IDefPropFloat : IDefBase
        {
            float PDef { get; set; }
        }

        public interface IDefPropNullableFloat : IDefBase
        {
            float? PDef { get; set; }
        }

        [TestCase( typeof( IDefPropInt ), typeof( IDefPropFloat ) )]
        [TestCase( typeof( IDefPropNullableFloat ), typeof( IDefPropFloat ) )]
        public void same_Poco_properties_when_not_Poco_family_must_be_exactly_the_same( Type t1, Type t2 )
        {
            var c = TestHelper.CreateStObjCollector( t1, t2 );
            TestHelper.GetFailedResult( c, "Type must be exactly '", "' since '", "' defines it." );
        }

        public interface IDefTestMaskedBaseProperties : IDefTest
        {
            [DefaultValue( 3713 )]
            new int PDef { get; }

            [DefaultValue( "Hello World!" )]
            new string Message { get; }
        }

        [Test]
        public void DefaultValueAttribute_must_be_the_same_when_base_properties_are_masked()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IDefTestMaskedBaseProperties ) );
            TestHelper.GetFailedResult( c, "Default values difference between 'CK.StObj.Engine.Tests.Poco.PocoTests+IDefTest.PDef' = '3712' and 'CK.StObj.Engine.Tests.Poco.PocoTests+IDefTestMaskedBaseProperties.PDef' = '3713'." );
        }

        public interface IDefBase : IPoco
        {
        }

        public interface IDef1 : IDefBase
        {
            [DefaultValue( 3712 )]
            int PDef { get; }

            [DefaultValue( "Hello \"World\"!" )]
            string Message { get; }
        }

        public interface IDef2 : IDefBase
        {
            [DefaultValue( 3713 )]
            int PDef { get; }

            [DefaultValue( "Hello World!" )]
            string Message { get; }
        }

        [Test]
        public void DefaultValueAttribute_must_be_the_same_accross_the_different_interfaces()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IDef1 ), typeof( IDef2 ) );
            TestHelper.GetFailedResult( c, "Default values difference between 'CK.StObj.Engine.Tests.Poco.PocoTests+IDef1.PDef' = '3712' and 'CK.StObj.Engine.Tests.Poco.PocoTests+IDef2.PDef' = '3713'." );
        }

        public interface IInvalidDefaultValue1 : IPoco
        {
            [DefaultValue( "bug" )]
            IDef1 Auto { get; }
        }

        public interface IInvalidDefaultValue2 : IPoco
        {
            [DefaultValue( 3712 )]
            string Auto { get; }
        }

        public interface IInvalidDefaultValue3 : IPoco
        {
            [DefaultValue( "" )]
            int Auto { get; }
        }

        public interface IInvalidDefaultValue4 : IPoco
        {
            [DefaultValue( typeof(DateTime), "2021-01-31" )]
            int Auto { get; }
        }

        [TestCase( typeof( IInvalidDefaultValue1 ) )]
        [TestCase( typeof( IInvalidDefaultValue2 ) )]
        [TestCase( typeof( IInvalidDefaultValue3 ) )]
        [TestCase( typeof( IInvalidDefaultValue4 ) )]
        public void DefaultValueAttribute_and_property_type_must_match( Type t )
        {
            var c = TestHelper.CreateStObjCollector( t, typeof( IDef1 ) );
            TestHelper.GetFailedResult( c, "Invalid DefaultValue attribute" );
        }

        public interface IRootTest : IPoco
        {
            ISubTest Sub { get; set; }
        }

        public interface ISubTest : IPoco
        {
            string SubMessage { get; set; }
        }

        public interface IRootBetterTest : IRootTest
        {
            new ISubBestTest Sub { get; set; }
        }

        public interface IRootBestTest : IRootBetterTest
        {
            new ISubBestTest Sub { get; set; }
        }

        public interface ISubBetterTest : ISubTest
        {
            string SubBetterMessage { get; set; }
        }

        public interface ISubBestTest : ISubBetterTest
        {
            string SubBestMessage { get; set; }
        }


        [Test]
        public void same_Poco_properties_can_be_of_any_type_as_long_as_they_belong_to_the_same_Poco_family()
        {
            {
                var c = TestHelper.CreateStObjCollector( typeof( IRootTest ), typeof( ISubTest ), typeof( IRootBestTest ), typeof( ISubBestTest ) );
                TestHelper.GetSuccessfulResult( c );
            }

            {
                var c = TestHelper.CreateStObjCollector( typeof( IRootTest ),
                                                         typeof( ISubTest ),
                                                         typeof( IRootBestTest ),
                                                         typeof( ISubBestTest ),
                                                         typeof( IRootAbsoluteBestTest ) );
                TestHelper.GetSuccessfulResult( c );
            }

            // Without registering the IDefBase Poco:
            {
                var c = TestHelper.CreateStObjCollector( typeof( IRootTest ),
                                                         typeof( ISubTest ),
                                                         typeof( IRootBestTest ),
                                                         typeof( ISubBestTest ),
                                                         typeof( IRootAbsoluteBestTest ),
                                                         typeof( IRootBuggyOtherFamily ) );
                TestHelper.GetFailedResult( c, "IPoco 'CK.StObj.Engine.Tests.Poco.PocoTests+IDefBase' has been excluded." );
            }

            // With IDefBase Poco registration:
            {
                var c = TestHelper.CreateStObjCollector( typeof( IRootTest ),
                                                         typeof( ISubTest ),
                                                         typeof( IRootBestTest ),
                                                         typeof( ISubBestTest ),
                                                         typeof( IRootAbsoluteBestTest ),
                                                         typeof( IRootBuggyOtherFamily ),
                                                         typeof( IDefBase ) );
                TestHelper.GetFailedResult( c, "Property 'CK.StObj.Engine.Tests.Poco.PocoTests.IRootBuggyOtherFamily.Sub': Type must be exactly 'CK.StObj.Engine.Tests.Poco.PocoTests.ISubTest' since 'CK.StObj.Engine.Tests.Poco.PocoTests.IRootTest.Sub' defines it." );
            }
        }

        public interface IRootAbsoluteBestTest : IRootBestTest
        {
            new ISubBetterTest Sub { get; set; }
        }

        public interface IRootBuggyOtherFamily : IRootTest
        {
            new IDefBase Sub { get; set; }
        }


    }
}
