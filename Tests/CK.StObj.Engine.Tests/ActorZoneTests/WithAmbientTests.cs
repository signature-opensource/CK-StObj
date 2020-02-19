using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using CK.Setup;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.ActorZoneTests
{
    [TestFixture]
    public class WithAmbientTests
    {

        internal static void CheckChildren<T>( IStObjObjectEngineMap map, string childrenTypeNames )
        {
            IEnumerable<IStObjResult> items = map.ToStObj( typeof( T ) ).Children;
            var s1 = items.Select( i => i.ObjectType.Name ).OrderBy( Util.FuncIdentity );
            var s2 = childrenTypeNames.Split( ',' ).OrderBy( Util.FuncIdentity );
            if( !s1.SequenceEqual( s2 ) )
            {
                Assert.Fail( "Expecting '{0}' but was '{1}'.", String.Join( ", ", s2 ), String.Join( ", ", s1 ) );
            }
        }


        public class AmbientPropertySetAttribute : Attribute, IStObjStructuralConfigurator
        {
            public string PropertyName { get; set; }

            public object PropertyValue { get; set; }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                o.SetAmbientPropertyValue( monitor, PropertyName, PropertyValue, "AmbientPropertySetAttribute" );
            }
        }

        [StObj( ItemKind = DependentItemKindSpec.Group, TrackAmbientProperties = TrackAmbientPropertiesMode.AddPropertyHolderAsChildren )]
        public class SqlDatabaseDefault : IRealObject
        {
            void StObjConstruct( string connectionString )
            {
                ConnectionString = connectionString;
            }

            public string ConnectionString { get; private set; }
        }

        [CKTypeDefiner]
        public class BaseDatabaseObject : IRealObject
        {
            [AmbientProperty]
            public SqlDatabaseDefault Database { get; set; }
            
            [AmbientProperty]
            public string Schema { get; set; }
        }

        #region Basic Package

        // We want BasicActor, BasicUser and BasicGroup to be in CK schema since they belong to BasicPackage.
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        [AmbientPropertySet( PropertyName = "Schema", PropertyValue = "CK" )]
        public class BasicPackage : BaseDatabaseObject
        {
            [InjectObject]
            public BasicUser UserHome { get; protected set; }
            
            [InjectObject]
            public BasicGroup GroupHome { get; protected set; }
        }

        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicActor : BaseDatabaseObject
        {
        }

        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicUser : BaseDatabaseObject
        {
        }

        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicGroup : BaseDatabaseObject
        {
            void StObjConstruct( BasicActor actor )
            {
            }
        }

        #endregion

        #region Zone Package

        // ZonePackage specializes BasicPackage. Its Schema is the same as BasicPackage (CK).
        public class ZonePackage : BasicPackage
        {
            [InjectObject]
            public new ZoneGroup GroupHome { get { return (ZoneGroup)base.GroupHome; } }
        }

        [StObj( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public class ZoneGroup : BasicGroup
        {
            void StObjConstruct( SecurityZone zone )
            {
            }
        }

        // This new object in ZonePackage will be in CK schema.
        [StObj( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public class SecurityZone : BaseDatabaseObject
        {
            void StObjConstruct( BasicGroup group )
            {
            }
        }

        #endregion

        #region Authentication Package

        // This new Package introduces a new Schema: CKAuth.
        // The objects that are specializations of objects from other packages must stay in CK.
        // But a new object like AuthenticationDetail must be in CKAuth.
        [StObj( ItemKind = DependentItemKindSpec.Container )]
        [AmbientPropertySet( PropertyName = "Schema", PropertyValue = "CKAuth" )]
        public class AuthenticationPackage : BaseDatabaseObject
        {
        }

        [StObj( Container = typeof( AuthenticationPackage ) )]
        public class AuthenticationUser : BasicUser
        {
        }

        [StObj( Container = typeof( AuthenticationPackage ) )]
        public class AuthenticationDetail : BaseDatabaseObject
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
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer(), valueResolver: valueResolver );
            collector.RegisterType( typeof( BasicPackage ) );
            collector.RegisterType( typeof( BasicActor ) );
            collector.RegisterType( typeof( BasicUser ) );
            collector.RegisterType( typeof( BasicGroup ) );
            collector.RegisterType( typeof( ZonePackage ) );
            collector.RegisterType( typeof( ZoneGroup ) );
            collector.RegisterType( typeof( SecurityZone ) );
            collector.RegisterType( typeof( AuthenticationPackage ) );
            collector.RegisterType( typeof( AuthenticationUser ) );
            collector.RegisterType( typeof( AuthenticationDetail ) );
            collector.RegisterType( typeof( SqlDatabaseDefault ) );

            collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
            collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );

            StObjCollectorResult r = collector.GetResult();
            Assert.That( r.HasFatalError, Is.False );
            CheckChildren<BasicPackage>( r.StObjs, "BasicActor,BasicUser,BasicGroup" );
            CheckChildren<ZonePackage>( r.StObjs, "SecurityZone,ZoneGroup" );
            CheckChildren<SqlDatabaseDefault>( r.StObjs, "BasicPackage,BasicActor,BasicUser,BasicGroup,ZonePackage,SecurityZone,ZoneGroup,AuthenticationPackage,AuthenticationUser,AuthenticationDetail" );

            var basicPackage = r.StObjs.Obtain<BasicPackage>();
            Assert.That( basicPackage is ZonePackage );
            Assert.That( basicPackage.GroupHome is ZoneGroup );
            Assert.That( basicPackage.Schema, Is.EqualTo( "CK" ) );

            var authenticationUser = r.StObjs.Obtain<AuthenticationUser>();
            Assert.That( authenticationUser.Schema, Is.EqualTo( "CK" ) );
            
            var authenticationDetail = r.StObjs.Obtain<AuthenticationDetail>();
            Assert.That( authenticationDetail.Schema, Is.EqualTo( "CKAuth" ) );
        }
    }
}
