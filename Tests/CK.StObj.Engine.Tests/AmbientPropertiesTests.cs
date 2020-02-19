using System;
using System.Linq;
using CK.Core;
using CK.Setup;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests
{
    [TestFixture]
    public partial class AmbientPropertiesTests
    {
        public class AmbientPropertySetAttribute : Attribute, IStObjStructuralConfigurator
        {
            public string PropertyName { get; set; }

            public object PropertyValue { get; set; }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                o.SetAmbientPropertyValue( monitor, PropertyName, PropertyValue, "AmbientPropertySetAttribute" );
            }
        }

        public class DirectPropertySetAttribute : Attribute, IStObjStructuralConfigurator
        {
            public string PropertyName { get; set; }

            public object PropertyValue { get; set; }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                o.SetDirectPropertyValue( monitor, PropertyName, PropertyValue, "DirectPropertySetAttribute" );
            }
        }

        [DirectPropertySet( PropertyName = "OneIntValue", PropertyValue = 3712 )]
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class SimpleObjectDirect : IRealObject
        {
            public int OneIntValue { get; set; }
        }

        [AmbientPropertySet( PropertyName = "OneIntValue", PropertyValue = 3712 )]
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class SimpleObjectAmbient : IRealObject
        {
            [AmbientProperty]
            public int OneIntValue { get; set; }
        }

        class ConfiguratorOneIntValueSetTo42 : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ObjectType == typeof( SimpleObjectDirect ) )
                {
                    o.SetDirectPropertyValue( monitor, "OneIntValue", 42, "ConfiguratorOneIntValueSetTo42" );
                }
                if( o.ObjectType == typeof( SimpleObjectAmbient ) )
                {
                    o.SetAmbientPropertyValue( monitor, "OneIntValue", 42, "ConfiguratorOneIntValueSetTo42" );
                }
            }
        }


        #region Only one object.

        [Test]
        public void OneObjectDirectProperty()
        {
            var container = new SimpleServiceContainer();
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container );
                collector.RegisterType( typeof( SimpleObjectDirect ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( result.OrderedStObjs.FirstOrDefault(), Is.Not.Null, "We registered SimpleObjectDirect." );
                Assert.That( result.OrderedStObjs.First().InitialObject, Is.InstanceOf<SimpleObjectDirect>() );
                Assert.That( ((SimpleObjectDirect)result.OrderedStObjs.First().InitialObject).OneIntValue, Is.EqualTo( 3712 ), "Direct properties can be set by Attribute." );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container, configurator: new ConfiguratorOneIntValueSetTo42() );
                collector.RegisterType( typeof( SimpleObjectDirect ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( ((SimpleObjectDirect)result.OrderedStObjs.First().InitialObject).OneIntValue, Is.EqualTo( 42 ), "Direct properties can be set by any IStObjStructuralConfigurator participant (here the global one)." );
            }
        }

        [Test]
        public void OneObjectAmbientProperty()
        {
            var container = new SimpleServiceContainer();
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container );
                collector.RegisterType( typeof( SimpleObjectAmbient ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( result.OrderedStObjs.FirstOrDefault(), Is.Not.Null, "We registered SimpleObjectAmbient." );
                Assert.That( result.OrderedStObjs.First().InitialObject, Is.InstanceOf<SimpleObjectAmbient>() );
                Assert.That( ((SimpleObjectAmbient)result.OrderedStObjs.First().InitialObject).OneIntValue, Is.EqualTo( 3712 ), "Same as Direct properties (above) regarding direct setting. The difference between Ambient and non-ambient lies in value propagation." );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container, configurator: new ConfiguratorOneIntValueSetTo42() );
                collector.RegisterType( typeof( SimpleObjectAmbient ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( ((SimpleObjectAmbient)result.OrderedStObjs.First().InitialObject).OneIntValue, Is.EqualTo( 42 ), "Same as Direct properties (above) regarding direct setting. The difference between Ambient and non-ambient lies in value propagation." );
            }
        }

        #endregion


        [DirectPropertySet( PropertyName = "OneIntValue", PropertyValue = 999 )]
        public class SpecializedObjectDirect : SimpleObjectDirect
        {
        }

        [AmbientPropertySet( PropertyName = "OneIntValue", PropertyValue = 999 )]
        public class SpecializedObjectAmbient : SimpleObjectAmbient
        {
        }


        [Test]
        public void AmbientOrDirectPropertyDeclaredInBaseClassCanBeSet()
        {
            var container = new SimpleServiceContainer();
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container );
                collector.RegisterType( typeof( SpecializedObjectDirect ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( result.OrderedStObjs.Count, Is.EqualTo( 2 ), "SpecializedObjectDirect and SimpleObjectDirect." );
                Assert.That( result.StObjs.Obtain<SpecializedObjectDirect>().OneIntValue, Is.EqualTo( 999 ), "Direct properties can be set by Attribute (or any IStObjStructuralConfigurator)." );
            }
            {
                StObjCollector collector = new StObjCollector( TestHelper.Monitor, container );
                collector.RegisterType( typeof( SpecializedObjectAmbient ) );
                StObjCollectorResult result = collector.GetResult( );
                Assert.That( result.OrderedStObjs.Count, Is.EqualTo( 2 ), "SpecializedObjectAmbient and SimpleObjectAmbient." );
                Assert.That( result.StObjs.Obtain<SpecializedObjectAmbient>().OneIntValue, Is.EqualTo( 999 ), "Ambient properties can be set by Attribute (or any IStObjStructuralConfigurator)." );
            }
        }

        #region Propagation to container's children.

        [StObj( Container = typeof( SimpleObjectDirect ) )]
        public class SimpleObjectInsideDirect : IRealObject
        {
            [AmbientProperty]
            public int OneIntValue { get; set; }
        }

        [StObj( Container = typeof( SimpleObjectAmbient ) )]
        public class SimpleObjectInsideAmbient : IRealObject
        {
            [AmbientProperty]
            public int OneIntValue { get; set; }
        }

        [Test]
        public void PropagationFromDirectPropertyDoesNotWork()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new ConfiguratorOneIntValueSetTo42() );
            collector.RegisterType( typeof( SimpleObjectDirect ) );
            collector.RegisterType( typeof( SimpleObjectInsideDirect ) );
            StObjCollectorResult result = collector.GetResult();
            Assert.That( result.StObjs.Obtain<SimpleObjectInsideDirect>().OneIntValue, Is.EqualTo( 0 ), "A direct property (not an ambient property) CAN NOT be a source for ambient properties." );
            Assert.That( result.StObjs.Obtain<SimpleObjectDirect>().OneIntValue, Is.EqualTo( 42 ), "...But it can be set by any IStObjStructuralConfigurator participant." );
        }

        [Test]
        public void PropagationFromAmbientProperty()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new ConfiguratorOneIntValueSetTo42() );
            collector.RegisterType( typeof( SimpleObjectAmbient ) );
            collector.RegisterType( typeof( SimpleObjectInsideAmbient ) );
            StObjCollectorResult result = collector.GetResult();
            Assert.That( result.StObjs.Obtain<SimpleObjectInsideAmbient>().OneIntValue, Is.EqualTo( 42 ), "Of course, ambient properties propagate their values." );
        }

        #endregion

        #region Potentially recursive resolution with type resolution

        public class BaseForObject
        {
            [AmbientProperty]
            public TypeToMapBase Ambient { get; set; }
        }

        public class TypeToMapBase
        {
        }

        public class TypeToMap : TypeToMapBase, IRealObject
        {
        }

        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class C1 : BaseForObject, IRealObject
        {
        }

        [StObj( Container = typeof( C1 ) )]
        public class O1InC1 : BaseForObject, IRealObject
        {
        }

        public class C2 : C1
        {
        }

        [StObj( Container = typeof( C2 ) )]
        public class O2InC2 : O1InC1
        {
        }

        public class AmbientResolutionTypeSetter : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ObjectType == typeof( C1 ) ) o.SetAmbientPropertyConfiguration( monitor, "Ambient", typeof(TypeToMap), StObjRequirementBehavior.ErrorIfNotStObj );
            }
        }


        [Test]
        public void TypeResolution()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), configurator: new AmbientResolutionTypeSetter() );
            collector.RegisterType( typeof( O2InC2 ) );
            collector.RegisterType( typeof( C2 ) );
            collector.RegisterType( typeof( TypeToMap ) );
            var result = collector.GetResult( );
            Assert.That( result.HasFatalError, Is.False );
            TypeToMap o = result.StObjs.Obtain<TypeToMap>();
            Assert.That( result.StObjs.Obtain<C1>().Ambient, Is.SameAs( o ) );
            Assert.That( result.StObjs.Obtain<O1InC1>().Ambient, Is.SameAs( o ) );

            Assert.That( result.StObjs.Obtain<C2>(), Is.SameAs( result.StObjs.Obtain<C1>() ) );
            Assert.That( result.StObjs.Obtain<O2InC2>(), Is.SameAs( result.StObjs.Obtain<O1InC1>() ) );
        }
        
        #endregion

    }
}
