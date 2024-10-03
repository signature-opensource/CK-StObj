using System;
using System.Diagnostics;
using System.Linq;
using CK.Core;
using CK.Setup;
using CK.Testing;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable CA1018 // Mark attributes with AttributeUsageAttribute

namespace CK.StObj.Engine.Tests;

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
    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class SimpleObjectDirect : IRealObject
    {
        public int OneIntValue { get; set; }
    }

    [AmbientPropertySet( PropertyName = "OneIntValue", PropertyValue = 3712 )]
    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class SimpleObjectAmbient : IRealObject
    {
        [AmbientProperty]
        public int OneIntValue { get; set; }
    }

    class ConfiguratorOneIntValueSetTo42 : IStObjStructuralConfigurator
    {
        public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
        {
            if( o.ClassType == typeof( SimpleObjectDirect ) )
            {
                o.SetDirectPropertyValue( monitor, "OneIntValue", 42, "ConfiguratorOneIntValueSetTo42" );
            }
            if( o.ClassType == typeof( SimpleObjectAmbient ) )
            {
                o.SetAmbientPropertyValue( monitor, "OneIntValue", 42, "ConfiguratorOneIntValueSetTo42" );
            }
        }
    }


    #region Only one object.

    [Test]
    public void OneObjectDirectProperty()
    {
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SimpleObjectDirect )] ).EngineMap;
            Throw.DebugAssert( map != null );
            map.StObjs.Obtain<SimpleObjectDirect>()!.OneIntValue.Should().Be( 3712, "Direct properties can be set by Attribute." );
        }
        {
            var container = new SimpleServiceContainer();
            StObjCollector collector = new StObjCollector( container, configurator: new ConfiguratorOneIntValueSetTo42() );
            collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectDirect ) );
            var map = collector.GetResult( TestHelper.Monitor ).EngineMap;
            Throw.DebugAssert( map != null );
            map.StObjs.Obtain<SimpleObjectDirect>()!.OneIntValue.Should().Be( 42, "Direct properties can be set by any IStObjStructuralConfigurator participant (here the global one)." );
        }
    }

    [Test]
    public void OneObjectAmbientProperty()
    {
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SimpleObjectAmbient )] ).EngineMap;
            Throw.DebugAssert( map != null );
            map.StObjs.OrderedStObjs.Should().NotBeEmpty( "We registered SimpleObjectAmbient." );
            map.StObjs.Obtain<SimpleObjectAmbient>()!.OneIntValue.Should().Be( 3712, "Same as Direct properties (above) regarding direct setting. The difference between Ambient and non-ambient lies in value propagation." );
        }
        {
            StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new ConfiguratorOneIntValueSetTo42() );
            collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectAmbient ) );

            collector.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
            StObjCollectorResult? r = collector.GetResult( TestHelper.Monitor );
            r.HasFatalError.Should().Be( false, "There must be no error." );

            var map = r.EngineMap;
            Throw.DebugAssert( map != null );
            map.StObjs.Obtain<SimpleObjectAmbient>()!.OneIntValue.Should().Be( 42, "Same as Direct properties (above) regarding direct setting. The difference between Ambient and non-ambient lies in value propagation." );
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
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SpecializedObjectDirect )] ).EngineMap;
            Throw.DebugAssert( map != null );

            map.StObjs.OrderedStObjs.Select( o => o.ClassType ).Should().Contain( new[] { typeof( SpecializedObjectDirect ), typeof( SimpleObjectDirect ) } );
            map.StObjs.Obtain<SpecializedObjectDirect>()!.OneIntValue.Should().Be( 999, "Direct properties can be set by Attribute (or any IStObjStructuralConfigurator)." );
        }
        {
            var map = TestHelper.GetSuccessfulCollectorResult( [typeof( SpecializedObjectAmbient )] ).EngineMap;
            Throw.DebugAssert( map != null );

            map.StObjs.OrderedStObjs.Select( o => o.ClassType ).Should().Contain( new[] { typeof( SpecializedObjectAmbient ), typeof( SimpleObjectAmbient ) } );
            map.StObjs.Obtain<SpecializedObjectAmbient>()!.OneIntValue.Should().Be( 999, "Ambient properties can be set by Attribute (or any IStObjStructuralConfigurator)." );
        }
    }

    #region Propagation to container's children.

    [RealObject( Container = typeof( SimpleObjectDirect ) )]
    public class SimpleObjectInsideDirect : IRealObject
    {
        [AmbientProperty]
        public int OneIntValue { get; set; }
    }

    [RealObject( Container = typeof( SimpleObjectAmbient ) )]
    public class SimpleObjectInsideAmbient : IRealObject
    {
        [AmbientProperty]
        public int OneIntValue { get; set; }
    }

    [Test]
    public void PropagationFromDirectPropertyDoesNotWork()
    {
        StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new ConfiguratorOneIntValueSetTo42() );
        collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectDirect ) );
        collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectInsideDirect ) );

        collector.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
        StObjCollectorResult? r = collector.GetResult( TestHelper.Monitor );
        r.HasFatalError.Should().Be( false, "There must be no error." );

        var map = r.EngineMap;
        Throw.DebugAssert( map != null );

        Assert.That( map.StObjs.Obtain<SimpleObjectInsideDirect>()!.OneIntValue, Is.EqualTo( 0 ), "A direct property (not an ambient property) CAN NOT be a source for ambient properties." );
        Assert.That( map.StObjs.Obtain<SimpleObjectDirect>()!.OneIntValue, Is.EqualTo( 42 ), "...But it can be set by any IStObjStructuralConfigurator participant." );
    }

    [Test]
    public void PropagationFromAmbientProperty()
    {
        StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new ConfiguratorOneIntValueSetTo42() );
        collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectAmbient ) );
        collector.RegisterType( TestHelper.Monitor, typeof( SimpleObjectInsideAmbient ) );

        collector.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
        StObjCollectorResult? r = collector.GetResult( TestHelper.Monitor );
        r.HasFatalError.Should().Be( false, "There must be no error." );

        var map = r.EngineMap;
        Throw.DebugAssert( map != null );
        Assert.That( map.StObjs.Obtain<SimpleObjectInsideAmbient>()!.OneIntValue, Is.EqualTo( 42 ), "Of course, ambient properties propagate their values." );
    }

    #endregion

    #region Potentially recursive resolution with type resolution

    public class BaseForObject
    {
        [AmbientProperty]
        public TypeToMapBase? Ambient { get; set; }
    }

    public class TypeToMapBase
    {
    }

    public class TypeToMap : TypeToMapBase, IRealObject
    {
    }

    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class C1 : BaseForObject, IRealObject
    {
    }

    [RealObject( Container = typeof( C1 ) )]
    public class O1InC1 : BaseForObject, IRealObject
    {
    }

    public class C2 : C1
    {
    }

    [RealObject( Container = typeof( C2 ) )]
    public class O2InC2 : O1InC1
    {
    }

    public class AmbientResolutionTypeSetter : IStObjStructuralConfigurator
    {
        public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
        {
            if( o.ClassType == typeof( C1 ) ) o.SetAmbientPropertyConfiguration( monitor, "Ambient", typeof(TypeToMap), StObjRequirementBehavior.ErrorIfNotStObj );
        }
    }


    [Test]
    public void TypeResolution()
    {
        StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: new AmbientResolutionTypeSetter() );
        collector.RegisterType( TestHelper.Monitor, typeof( O2InC2 ) );
        collector.RegisterType( TestHelper.Monitor, typeof( C2 ) );
        collector.RegisterType( TestHelper.Monitor, typeof( TypeToMap ) );
        collector.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
        StObjCollectorResult? r = collector.GetResult( TestHelper.Monitor );
        r.HasFatalError.Should().Be( false, "There must be no error." );

        var map = r.EngineMap;
        Throw.DebugAssert( map != null );

        TypeToMap o = map.StObjs.Obtain<TypeToMap>()!;
        Assert.That( map.StObjs.Obtain<C1>()!.Ambient, Is.SameAs( o ) );
        Assert.That( map.StObjs.Obtain<O1InC1>()!.Ambient, Is.SameAs( o ) );

        Assert.That( map.StObjs.Obtain<C2>(), Is.SameAs( map.StObjs.Obtain<C1>() ) );
        Assert.That( map.StObjs.Obtain<O2InC2>(), Is.SameAs( map.StObjs.Obtain<O1InC1>() ) );
    }
    
    #endregion

}
