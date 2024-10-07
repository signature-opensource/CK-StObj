using System;
using System.Diagnostics;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.ActorZoneTests;

[TestFixture]
public class WithoutAmbientTests
{
    // This is not how the real SqlDefaultDatabase is implemented: see WithAmbientTests for a more accurate reproduction.
    [RealObject( ItemKind = DependentItemKindSpec.Group,
            Children =
            [
                typeof( BasicPackage ),
                typeof( BasicActor ),
                typeof( BasicUser ),
                typeof( BasicGroup ),
                typeof( ZonePackage ),
                typeof( ZoneGroup ),
                typeof( SecurityZone ),
                typeof( AuthenticationPackage ),
                typeof( AuthenticationUser )
            ] )]
    public class SqlDefaultDatabase : IRealObject
    {
        public string? ConnectionString { get; private set; }

        // We'll use the ValueResolver below to set the parameter.
        void StObjConstruct( string connectionString )
        {
            ConnectionString = connectionString;
        }
    }

    #region Basic Package

    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class BasicPackage : IRealObject
    {
    }

    [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
    public class BasicActor : IRealObject
    {
    }


    [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
    public class BasicUser : IRealObject
    {
    }


    [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
    public class BasicGroup : IRealObject
    {
        void StObjConstruct( BasicActor actor )
        {
        }
    }

    #endregion

    #region Zone Package

    public class ZonePackage : BasicPackage
    {
    }

    [RealObject( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
    public class ZoneGroup : BasicGroup
    {
        void StObjConstruct( ISecurityZone zone )
        {
        }
    }

    public interface ISecurityZone : IRealObject
    {
    }

    [RealObject( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
    public class SecurityZone : ISecurityZone
    {
        void StObjConstruct( BasicGroup group )
        {
        }
    }

    #endregion

    #region Authentication Package

    [RealObject( ItemKind = DependentItemKindSpec.Container )]
    public class AuthenticationPackage : IRealObject
    {
    }

    [RealObject( Container = typeof( AuthenticationPackage ) )]
    public class AuthenticationUser : BasicUser
    {
    }

    #endregion

    public class ValueResolver : IStObjValueResolver
    {
        public void ResolveExternalPropertyValue( IActivityMonitor monitor, IStObjFinalAmbientProperty ambientProperty )
        {
        }

        public void ResolveParameterValue( IActivityMonitor monitor, IStObjFinalParameter parameter )
        {
            if( parameter.Name == "connectionString" && parameter.Type == typeof( string ) )
            {
                parameter.SetParameterValue( "The connection String" );
            }
        }
    }

    [Test]
    public void LayeredArchitecture()
    {
        var valueResolver = new ValueResolver();
        StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), valueResolver: valueResolver );
        collector.RegisterTypes( TestHelper.Monitor, new[] { typeof( BasicPackage ),
                                                             typeof( BasicActor ),
                                                             typeof( BasicUser ),
                                                             typeof( BasicGroup ),
                                                             typeof( ZonePackage ),
                                                             typeof( ZoneGroup ),
                                                             typeof( SecurityZone ),
                                                             typeof( AuthenticationPackage ),
                                                             typeof( AuthenticationUser ),
                                                             typeof( SqlDefaultDatabase ) } );
        collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
        collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );

        collector.FatalOrErrors.Count.Should().Be( 0, "There must be no registration error (CKTypeCollector must be successful)." );
        StObjCollectorResult? r = collector.GetResult( TestHelper.Monitor );
        r.HasFatalError.Should().Be( false, "There must be no error." );

        var map = r.EngineMap;
        Throw.DebugAssert( "No initialization error.", map != null );

        WithAmbientTests.CheckChildren<BasicPackage>( map.StObjs, "BasicActor,BasicUser,BasicGroup" );
        WithAmbientTests.CheckChildren<ZonePackage>( map.StObjs, "SecurityZone,ZoneGroup" );
        WithAmbientTests.CheckChildren<SqlDefaultDatabase>( map.StObjs, "BasicPackage,BasicActor,BasicUser,BasicGroup,ZonePackage,SecurityZone,ZoneGroup,AuthenticationPackage,AuthenticationUser" );
        var db = map.StObjs.Obtain<SqlDefaultDatabase>();
        Debug.Assert( db != null );
        db.ConnectionString.Should().Be( "The connection String" );
    }
}
