using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using CK.StObj.Engine.Tests.Poco;
using System.Linq;
using FluentAssertions;
using System.Diagnostics;
using System.Reflection;
using SmartAnalyzers.CSharpExtensions.Annotations;
using CK.StObj.Engine.Tests.Poco.Sample;
using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using static CK.Testing.StObjEngineTestHelper;
using System.Collections.Generic;

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

            [InitRequired]
            public IPocoFactory<IBasicPoco> Factory { get; private set; }

        }

        [Test]
        public void simple_poco_resolution_and_injection()
        {
            StObjCollectorResult result = BuildPocoSample( typeof( PackageWithBasicPoco ) );
            Debug.Assert( result.EngineMap != null );

            IStObjResult p = result.EngineMap.StObjs.ToHead( typeof( PackageWithBasicPoco ) );
            var package = (PackageWithBasicPoco)p.FinalImplementation.Implementation;
            package.Factory.Should().NotBeNull();
        }

        static StObjCollectorResult BuildPocoSample( params Type[] extra )
        {
            var types = typeof( PocoTests ).Assembly.GetTypes()
                            .Where( t => t.Namespace == "CK.StObj.Engine.Tests.Poco.Sample" )
                            .Concat( extra );

            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterTypes( types.ToList() );

            var result = collector.GetResult();
            Assert.That( result.HasFatalError, Is.False );
            return result;
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
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterType( typeof( IThing ) );
            collector.RegisteringFatalOrErrorCount.Should().Be( 0 );
            var poco = collector.GetResult().CKTypeResult.PocoSupport;
            Debug.Assert( poco != null, "Since there has been no error." );
            poco.Roots.Should().HaveCount( 1 );

            var TF = poco.Roots[0].PocoFactoryClass;
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
        public void poco_factory_exposes_the_final_type()
        {
            StObjCollectorResult result = BuildPocoSample();
            Debug.Assert( result.EngineMap != null, "No error." );

            var p = result.EngineMap.StObjs.Obtain<IPocoFactory<IBasicPoco>>();

            Type pocoType = p.PocoClassType;
            Assert.That( typeof( IBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEAlternateBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IECombineBasicPoco ).IsAssignableFrom( pocoType ) );
            Assert.That( typeof( IEIndependentBasicPoco ).IsAssignableFrom( pocoType ) );

        }

        public interface IDefTest : IPoco
        {
            [DefaultValue( 3712 )]
            int PDef { get; }

            [DefaultValue( "Hello \"World\"!" )]
            string Message { get; }
        }

        [Test]
        public void poco_property_supports_DefaultValueAttribute_from_System_ComponentModel()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IDefTest ) );
            var f = TestHelper.GetAutomaticServices( c ).Services.GetRequiredService<IPocoFactory<IDefTest>>();
            var o = f.Create();
            o.PDef.Should().Be( 3712 );
            o.Message.Should().Be( @"Hello ""World""!" );
        }

        public interface IDefPropInt : IDefBase
        {
            int PDef { get; }
        }

        public interface IDefPropFloat : IDefBase
        {
            float PDef { get; }
        }

        [Test]
        public void same_Poco_properties_when_not_Poco_family_must_be_exactly_the_same()
        {
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector( typeof( IDefPropInt ), typeof( IDefPropFloat ) ) );
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
            TestHelper.GetFailedResult( c );
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
            TestHelper.GetFailedResult( c );
        }

        public interface IInvalidNoDefaultValue : IPoco
        {
            [DefaultValue( "bug" )]
            IDef1 Auto { get; }
        }

        [Test]
        public void DefaultValueAttribute_must_not_exist_on_AutoInstantiated_properties()
        {
            var c = TestHelper.CreateStObjCollector( typeof( IInvalidNoDefaultValue ), typeof( IDef1 ) );
            TestHelper.GetFailedResult( c );
        }

        public interface IRootTest : IPoco
        {
            ISubTest Sub { get; }
        }

        public interface ISubTest : IPoco
        {
            string SubMessage { get; }
        }

        public interface IRootBetterTest : IRootTest
        {
            new ISubBestTest Sub { get; }
        }

        public interface IRootBestTest : IRootBetterTest
        {
            new ISubBestTest Sub { get; }
        }

        public interface IRootAbsoluteBestTest : IRootBestTest
        {
            new ISubBetterTest Sub { get; }
        }

        public interface ISubBetterTest : ISubTest
        {
            string SubBetterMessage { get; }
        }

        public interface ISubBestTest : ISubBetterTest
        {
            string SubBestMessage { get; }
        }

        public interface IRootBuggyOtherFamily : IRootTest
        {
            new IDefBase Sub { get; }
        }


        [Test]
        public void same_Poco_properties_can_be_of_any_type_as_long_as_they_belong_to_the_same_Poco_family()
        {
            TestHelper.GetSuccessfulResult( TestHelper.CreateStObjCollector(
                typeof( IRootTest ), typeof( ISubTest ), typeof( IRootBestTest ), typeof( ISubBestTest ) ) );

            TestHelper.GetSuccessfulResult( TestHelper.CreateStObjCollector(
                typeof( IRootTest ), typeof( ISubTest ), typeof( IRootBestTest ), typeof( ISubBestTest ), typeof( IRootAbsoluteBestTest ) ) );

            // Without registering the IDefBase Poco:
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector(
                typeof( IRootTest ), typeof( ISubTest ), typeof( IRootBestTest ), typeof( ISubBestTest ), typeof( IRootAbsoluteBestTest ), typeof( IRootBuggyOtherFamily ) ) );

            // With IDefBase Poco registration:
            TestHelper.GetFailedResult( TestHelper.CreateStObjCollector(
                typeof( IRootTest ), typeof( ISubTest ), typeof( IRootBestTest ), typeof( ISubBestTest ), typeof( IRootAbsoluteBestTest ), typeof( IRootBuggyOtherFamily ), typeof( IDefBase ) ) );
        }


    }
}
