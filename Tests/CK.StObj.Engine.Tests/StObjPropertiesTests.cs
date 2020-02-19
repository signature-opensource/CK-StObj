using System;
using System.Linq;
using CK.Core;
using CK.Setup;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

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
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class SimpleContainer : IRealObject
        {
        }

        [Test]
        public void OneObject()
        {
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
                collector.RegisterType( typeof( SimpleContainer ) );
                StObjCollectorResult result = collector.GetResult();
                Assert.That( result.OrderedStObjs.First().GetStObjProperty( "OneIntValue" ), Is.EqualTo( 3712 ) );
            }
        }

        #region Mergeable & Propagation

        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class SpecializedContainer : SimpleContainer
        {
        }

        [StObj( Container = typeof( SpecializedContainer ), ItemKind = DependentItemKindSpec.Item )]
        public class BaseObject : IRealObject
        {
        }

        [StObj( ItemKind = DependentItemKindSpec.Item )]
        public class SpecializedObject : BaseObject
        {
        }

        [StObj( Container = typeof( SpecializedContainer ), ItemKind = DependentItemKindSpec.Item )]
        public class SpecializedObjectWithExplicitContainer : SpecializedObject
        {
        }

        class SchmurtzConfigurator : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ObjectType == typeof( SimpleContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "Root" ) );
                if( o.ObjectType == typeof( SpecializedContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "SpecializedContainer specializes Root" ) );
                if( o.ObjectType == typeof( BaseObject ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "BaseObject belongs to SpecializedContainer" ) );
                if( o.ObjectType == typeof( SpecializedObject ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "Finally: SpecializedObject inherits from BaseObject" ) );
                if( o.ObjectType == typeof( SpecializedObjectWithExplicitContainer ) ) o.SetStObjPropertyValue( monitor, "SchmurtzProp", new SchmurtzProperty( "SpecializedObjectWithExplicitContainer inherits from BaseObject BUT is directly associated to SpecializedContainer" ) );
            }
        }

        [Test]
        public void SchmurtzPropagation()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new SchmurtzConfigurator() );
            collector.RegisterType( typeof( SimpleContainer ) );
            collector.RegisterType( typeof( SpecializedContainer ) );
            collector.RegisterType( typeof( BaseObject ) );
            collector.RegisterType( typeof( SpecializedObject ) );
            collector.RegisterType( typeof( SpecializedObjectWithExplicitContainer ) );
            StObjCollectorResult result = collector.GetResult();

            Assert.That( result.OrderedStObjs.First( s => s.ObjectType == typeof( BaseObject ) ).GetStObjProperty( "SchmurtzProp" ).ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => BaseObject belongs to SpecializedContainer" ) );

            Assert.That( result.OrderedStObjs.First( s => s.ObjectType == typeof( SpecializedObject ) ).GetStObjProperty( "SchmurtzProp" ).ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => BaseObject belongs to SpecializedContainer => Finally: SpecializedObject inherits from BaseObject" ),
                "Here, we follow the Generalization link, since there is NO direct Container." );

            Assert.That( result.OrderedStObjs.First( s => s.ObjectType == typeof( SpecializedObjectWithExplicitContainer ) ).GetStObjProperty( "SchmurtzProp" ).ToString(),
                Is.EqualTo( "Root => SpecializedContainer specializes Root => SpecializedObjectWithExplicitContainer inherits from BaseObject BUT is directly associated to SpecializedContainer" ),
                "Here, we DO NOT follow the Generalization link, since the Container is set, the Container has the priority." );
        }

        #endregion
    }
}
