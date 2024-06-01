using System;
using System.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public class StObjPropertiesTests
    {
        public class StObjPropertySetAttribute : Attribute, IStObjStructuralConfigurator
        {
            public string PropertyName { get; set; }

            public object PropertyValue { get; set; }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                o.SetStObjPropertyValue( monitor, PropertyName, PropertyValue );
            }
        }

        [StObjPropertySetAttribute( PropertyName = "OneIntValue", PropertyValue = 3712 )]
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class SimpleContainer : IRealObject
        {
        }

        [Test]
        public void OneObject()
        {
            {
                var collector = TestHelper.CreateTypeCollector( typeof( SimpleContainer ) );
                var result = TestHelper.GetSuccessfulCollectorResult( collector ).EngineMap!.StObjs;
                result.OrderedStObjs
                      .Single( o => o.FinalImplementation.Implementation is SimpleContainer )
                      .GetStObjProperty( "OneIntValue" ).Should().Be( 3712 );
            }
        }

        #region Mergeable & Propagation

        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class SpecializedContainer : SimpleContainer
        {
        }

        [RealObject( Container = typeof( SpecializedContainer ), ItemKind = DependentItemKindSpec.Item )]
        public class BaseObject : IRealObject
        {
        }

        [RealObject( ItemKind = DependentItemKindSpec.Item )]
        public class SpecializedObject : BaseObject
        {
        }

        [RealObject( Container = typeof( SpecializedContainer ), ItemKind = DependentItemKindSpec.Item )]
        public class SpecializedObjectWithExplicitContainer : SpecializedObject
        {
        }

        class SchmurtzConfigurator : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ClassType == typeof( SimpleContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "Root" ) );
                if( o.ClassType == typeof( SpecializedContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "SpecializedContainer specializes Root" ) );
                if( o.ClassType == typeof( BaseObject ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "BaseObject belongs to SpecializedContainer" ) );
                if( o.ClassType == typeof( SpecializedObject ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "Finally: SpecializedObject inherits from BaseObject" ) );
                if( o.ClassType == typeof( SpecializedObjectWithExplicitContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "SpecializedObjectWithExplicitContainer inherits from BaseObject BUT is directly associated to SpecializedContainer" ) );
            }
        }

        [Test]
        public void SchmurtzPropagation()
        {
            StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new SchmurtzConfigurator() );
            collector.RegisterType( TestHelper.Monitor, typeof( SimpleContainer ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SpecializedContainer ) );
            collector.RegisterType( TestHelper.Monitor, typeof( BaseObject ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SpecializedObject ) );
            collector.RegisterType( TestHelper.Monitor, typeof( SpecializedObjectWithExplicitContainer ) );
            var result = collector.GetResult( TestHelper.Monitor ).EngineMap!.StObjs;

            Assert.That( result.OrderedStObjs.First( s => s.ClassType == typeof( BaseObject ) ).GetStObjProperty( "SchmurtzProp" )!.ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => BaseObject belongs to SpecializedContainer" ) );

            Assert.That( result.OrderedStObjs.First( s => s.ClassType == typeof( SpecializedObject ) ).GetStObjProperty( "SchmurtzProp" )!.ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => BaseObject belongs to SpecializedContainer => Finally: SpecializedObject inherits from BaseObject" ),
                "Here, we follow the Generalization link, since there is NO direct Container." );

            Assert.That( result.OrderedStObjs.First( s => s.ClassType == typeof( SpecializedObjectWithExplicitContainer ) ).GetStObjProperty( "SchmurtzProp" )!.ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => SpecializedObjectWithExplicitContainer inherits from BaseObject BUT is directly associated to SpecializedContainer" ),
                "Here, we DO NOT follow the Generalization link, since the Container is set, the Container has the priority." );
        }

        #endregion
    }
}
