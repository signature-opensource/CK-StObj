using System;
using System.Diagnostics;
using CK.Core;
using CK.Setup;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace CK.StObj.Engine.Tests;

[TestFixture]
public partial class AmbientPropertiesPropagationTests
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

    class FromContainerAndThenGeneralization
    {
        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "OnBaseObject" )]
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class BaseObjectAmbient : IRealObject
        {
            [AmbientProperty( ResolutionSource = PropertyResolutionSource.FromContainerAndThenGeneralization )]
            public string? OneStringValue { get; set; }
        }

        public class InheritedBaseObject : BaseObjectAmbient
        {
        }

        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "OnInheritedWithSet" )]
        public class InheritedBaseObjectWithSet : BaseObjectAmbient
        {
        }

        public class InheritedBaseObjectWithoutSet : InheritedBaseObjectWithSet
        {
        }

        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "OnAnotherContainer" )]
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class AnotherContainer : IRealObject
        {
            [AmbientProperty]
            public string? OneStringValue { get; set; }
        }

        class ConfiguratorOneStringValueSetToPouf : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ClassType == typeof( BaseObjectAmbient ) )
                {
                    o.SetAmbientPropertyValue( monitor, "OneStringValue", "Pouf", "ConfiguratorOneStringValueSetToPouf" );
                }
            }
        }

        public void DoTest()
        {
            var container = new SimpleServiceContainer();
            {
                StObjCollector collector = new StObjCollector( container, configurator: new ConfiguratorOneStringValueSetToPouf() );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObject>()!.OneStringValue, Is.EqualTo( "Pouf" ), "Since InheritedSimpleObject is a BaseObjectAmbient, it has been configured." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator: new ConfiguratorOneStringValueSetToPouf() );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithSet>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "More specialized InheritedSimpleObjectWithSet has been set." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObjectWithSet" ) o.Container.Type = typeof( AnotherContainer );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithSet ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container has changed." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithSet>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "Property does not change since it is set on the class itself." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObjectWithoutSet" ) o.Container.Type = typeof( AnotherContainer );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithoutSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithSet ) )!.Container, Is.Null, "Container of InheritedSimpleObjectWithSet has NOT changed (no container)." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithoutSet ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedSimpleObjectWithoutSet has changed." );

                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithoutSet>()!.OneStringValue, Is.EqualTo( "OnAnotherContainer" ), "Here, the container's value takes precedence since Property is NOT set on the class itself but on its Generalization." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObjectWithSet" ) o.Container.Type = typeof( AnotherContainer );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithoutSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithSet ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedSimpleObjectWithSet has changed." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithoutSet ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedSimpleObjectWithoutSet is inherited." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithoutSet ) )!.ConfiguredContainer, Is.Null, "Container is inherited, not directly configured for the object." );

                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithoutSet>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "The inherited value is used since container is (also) inherited." );
            }
        }

    }

    class FromGeneralizationAndThenContainer
    {
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class BaseObjectAmbient : IRealObject
        {
            [AmbientProperty]
            public string? OneStringValue { get; set; }
        }

        public class InheritedBaseObject : BaseObjectAmbient
        {
        }

        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "OnInheritedWithSet" )]
        public class InheritedBaseObjectWithSet : BaseObjectAmbient
        {
        }

        public class InheritedBaseObjectWithoutSet : InheritedBaseObjectWithSet
        {
        }

        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "OnAnotherContainer" )]
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class AnotherContainer : IRealObject
        {
            [AmbientProperty( IsOptional = true )]
            public string? OneStringValue { get; set; }
        }

        [RealObject( ItemKind = DependentItemKindSpec.Container, Container = typeof( ContainerForContainerForBaseObject ) )]
        public class ContainerForBaseObject : IRealObject
        {
        }

        [AmbientPropertySet( PropertyName = "OneStringValue", PropertyValue = "On Container of ContainerForBaseObject" )]
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        public class ContainerForContainerForBaseObject : IRealObject
        {
            [AmbientProperty( IsOptional = true )]
            public string? OneStringValue { get; set; }
        }

        class ConfiguratorOneStringValueSetToPouf : IStObjStructuralConfigurator
        {
            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.ClassType == typeof( BaseObjectAmbient ) )
                {
                    o.SetAmbientPropertyValue( monitor, "OneStringValue", "Pouf", "ConfiguratorOneStringValueSetToPouf" );
                }
            }
        }

        public void DoTest()
        {
            var container = new SimpleServiceContainer();
            {
                StObjCollector collector = new StObjCollector( container );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObject>()!.OneStringValue, Is.Null, "No configuration." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator: new ConfiguratorOneStringValueSetToPouf() );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObject>()!.OneStringValue, Is.EqualTo( "Pouf" ), "Since InheritedSimpleObject is a BaseObjectAmbient, it has been configured." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator: new ConfiguratorOneStringValueSetToPouf() );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithSet>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "More specialized InheritedSimpleObjectWithSet has been set." );
                Assert.That( result.StObjs.Obtain<BaseObjectAmbient>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "The property is the same for any StObj." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObjectWithoutSet" ) o.Container.Type = typeof( AnotherContainer );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObjectWithoutSet ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithSet ) )!.Container, Is.Null, "Container of InheritedSimpleObjectWithSet has NOT changed (no container)." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObjectWithoutSet ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedSimpleObjectWithoutSet has changed." );

                Assert.That( result.StObjs.Obtain<InheritedBaseObjectWithoutSet>()!.OneStringValue, Is.EqualTo( "OnInheritedWithSet" ), "Generalization's value takes precedence, Container's value is ignored." );
            }
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObject" ) o.Container.Type = typeof( AnotherContainer );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObject ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedBaseObject has changed." );
                Assert.That( result.StObjs.ToHead( typeof( BaseObjectAmbient ) )!.Container, Is.Null, "BaseObjectAmbient has no container..." );
                Assert.That( result.StObjs.Obtain<BaseObjectAmbient>()!.OneStringValue, Is.EqualTo( "OnAnotherContainer" ), "The value comes from the container of the specialized object since the value is not set anywhere else." );
            }
            // Same as before except that the value is set on the BaseObjectAmbient: 
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObject" ) o.Container.Type = typeof( AnotherContainer );
                                                    if( o.ClassType == typeof( BaseObjectAmbient ) )
                                                    {
                                                        o.SetAmbientPropertyValue( TestHelper.Monitor, "OneStringValue", "OnBaseObject", "Configurator" );
                                                    }

                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );
                Assert.That( result.StObjs.ToHead( typeof( InheritedBaseObject ) )!.Container!.ClassType.Name, Is.EqualTo( "AnotherContainer" ), "Container of InheritedBaseObject has changed." );
                Assert.That( result.StObjs.ToHead( typeof( BaseObjectAmbient ) )!.Container, Is.Null, "BaseObjectAmbient has no container..." );
                Assert.That( result.StObjs.Obtain<InheritedBaseObject>()!.OneStringValue, Is.EqualTo( "OnBaseObject" ), "The value comes from the Generalization." );
            }
            // Two containers: the one of the Generalization wins. 
            {
                StObjCollector collector = new StObjCollector( container, configurator:
                                                new StructuralConfiguratorHelper( o =>
                                                {
                                                    if( o.ClassType.Name == "InheritedBaseObject" ) o.Container.Type = typeof( AnotherContainer );
                                                    if( o.ClassType.Name == "BaseObjectAmbient" ) o.Container.Type = typeof( ContainerForBaseObject );
                                                } ) );
                collector.RegisterType( TestHelper.Monitor, typeof( AnotherContainer ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ContainerForContainerForBaseObject ) );
                collector.RegisterType( TestHelper.Monitor, typeof( ContainerForBaseObject ) );
                collector.RegisterType( TestHelper.Monitor, typeof( InheritedBaseObject ) );
                var result = collector.GetResult( TestHelper.Monitor ).EngineMap;
                Debug.Assert( result != null, "No initialization error." );

                Assert.That( result.StObjs.Obtain<InheritedBaseObject>()!.OneStringValue, Is.EqualTo( "On Container of ContainerForBaseObject" ), "The value comes from the Generalization's Container." );
            }
        }

    }

    [Test]
    public void ResolutionFromContainerAndThenGeneralization()
    {
        new FromContainerAndThenGeneralization().DoTest();
    }

    [Test]
    public void ResolutionFromGeneralizationAndThenContainer()
    {
        new FromGeneralizationAndThenContainer().DoTest();
    }
}
