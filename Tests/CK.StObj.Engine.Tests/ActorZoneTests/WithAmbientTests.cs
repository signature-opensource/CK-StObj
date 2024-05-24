using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CK.Core;
using CK.Setup;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.StObjEngineTestHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning disable IDE0051 // Remove unused private members

namespace CK.StObj.Engine.Tests.ActorZoneTests
{
    [TestFixture]
    public class WithAmbientTests
    {

        internal static void CheckChildren<T>( IStObjObjectEngineMap map, string childrenTypeNames )
        {
            IEnumerable<IStObjResult> items = map.ToHead( typeof( T ) )!.Children;
            var s1 = items.Select( i => i.ClassType.Name );
            var s2 = childrenTypeNames.Split( ',' );
            s1.Should().BeEquivalentTo( s2 );
        }

        [AttributeUsage( AttributeTargets.Class )]
        public class AmbientPropertySetAttribute : Attribute, IStObjStructuralConfigurator
        {
            public string PropertyName { get; set; }

            public object PropertyValue { get; set; }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                o.SetAmbientPropertyValue( monitor, PropertyName, PropertyValue, sourceDescription: "AmbientPropertySetAttribute" );
            }
        }

        [CKTypeDefiner]
        [RealObject( ItemKind = DependentItemKindSpec.Group, TrackAmbientProperties = TrackAmbientPropertiesMode.AddPropertyHolderAsChildren )]
        public class SqlDatabase : IRealObject
        {
            readonly string _name;
            bool _hasCKCore;
            bool _useSnapshotIsolation;

            public SqlDatabase( string name )
            {
                Throw.CheckNotNullOrWhiteSpaceArgument( name );
                _name = name;
            }

            public string Name => _name;
            public bool IsDefaultDatabase => Name == "db";
            public string? ConnectionString { get; set; }

            public bool HasCKCore
            {
                get => _hasCKCore | IsDefaultDatabase;
                // There is no setter in reality (StObjConstruct is used), this
                // is only here to show an alternative way to set the configuration by using SetDirectProperty.
                private set => _hasCKCore = value;
            }

            public bool UseSnapshotIsolation
            {
                get => _useSnapshotIsolation | IsDefaultDatabase;
                // There is no setter in reality (StObjConstruct is used), this
                // is only here to show an alternative way to set the configuration by using SetDirectProperty.
                private set => _useSnapshotIsolation = value;
            }

            void StObjConstruct( string? connectionString = null, bool hasCKCore = false, bool useSnapshotIsolation = false )
            {
                // If "UseSetDirectProperty" is used, the Connection string has been set on "db" and "histo".
                // We protect this case here because StObjConstruct is always called (with System.Type.Missing parameters that triggers
                // the use of the default values).
                if( ConnectionString == null )
                {
                    ConnectionString = connectionString;
                    _hasCKCore = hasCKCore;
                    _useSnapshotIsolation = useSnapshotIsolation;
                }
            }
        }

        public class SqlDefaultDatabase : SqlDatabase
        {
            public SqlDefaultDatabase()
                : base( "db" )
            {
            }
        }

        public class SqlHistoDatabase : SqlDatabase
        {
            public SqlHistoDatabase()
                : base( "histo" )
            {
            }
        }

        // This database is not configured by StObjConstruct parameters.
        public class SqlAlienDatabase : SqlDatabase
        {
            public SqlAlienDatabase()
                : base( "alien" )
            {
            }
        }

        /// <summary>
        /// This acts as the "SqlPackage".
        /// </summary>
        [CKTypeDefiner]
        public class BaseDatabaseObject : IRealObject
        {
            [AmbientProperty]
            public SqlDefaultDatabase? Database { get; set; }
            
            [AmbientProperty]
            public string? Schema { get; set; }
        }

        #region Basic Package

        // We want BasicActor, BasicUser and BasicGroup to be in CK schema since they belong to BasicPackage.
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        [AmbientPropertySet( PropertyName = "Schema", PropertyValue = "CK" )]
        public class BasicPackage : BaseDatabaseObject
        {
            [InjectObject]
            public BasicUser UserHome { get; private set; }
            
            [InjectObject]
            public BasicGroup GroupHome { get; private set; }
        }

        [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicActor : BaseDatabaseObject
        {
        }

        [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicUser : BaseDatabaseObject
        {
        }

        [RealObject( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicGroup : BaseDatabaseObject
        {
            void StObjConstruct( BasicActor actor )
            {
            }
        }

        #endregion

        #region Zone Package

        // ZonePackage specializes BasicPackage. Its Schema is the same as BasicPackage (CK).
        public abstract class ZonePackage : BasicPackage
        {
            [InjectObject]
            public new ZoneGroup GroupHome { get { return (ZoneGroup)base.GroupHome; } }
        }

        [RealObject( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public abstract class ZoneGroup : BasicGroup
        {
            void StObjConstruct( SecurityZone zone )
            {
            }
        }

        // This new object in ZonePackage will be in CK schema.
        [RealObject( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public abstract class SecurityZone : BaseDatabaseObject
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
        [RealObject( ItemKind = DependentItemKindSpec.Container )]
        [AmbientPropertySet( PropertyName = "Schema", PropertyValue = "CKAuth" )]
        public class AuthenticationPackage : BaseDatabaseObject
        {
        }

        [RealObject( Container = typeof( AuthenticationPackage ) )]
        public class AuthenticationUser : BasicUser
        {
        }

        [RealObject( Container = typeof( AuthenticationPackage ) )]
        public class AuthenticationDetail : BaseDatabaseObject
        {
        }

        #endregion

        // This is how the SqlAspect configures the database.
        // There is no "mode" in reality: StObjConstruct is used.
        public class ConfiguratorByStObjConstruct : IStObjStructuralConfigurator
        {
            readonly string _mode;

            public ConfiguratorByStObjConstruct( string mode )
            {
                _mode = mode;
            }

            public void Configure( IActivityMonitor monitor, IStObjMutableItem o )
            {
                if( o.InitialObject is SqlDatabase db && o.Generalization == null )
                {
                    if( _mode == "UseStObjConstruct" )
                    {
                        ConfigureByStObjConstruct( monitor, o, db );
                    }
                    else if( _mode == "UseSetDirectProperty" )
                    {
                        ConfigureByDirectSetProperties( monitor, o, db );
                    }
                }
            }

            // This is NOT how it works in CK.SqlServer.Setup.Engine: StObjConstruct is used.
            static void ConfigureByDirectSetProperties( IActivityMonitor monitor, IStObjMutableItem o, SqlDatabase db )
            {
                if( db.IsDefaultDatabase )
                {
                    o.SetDirectPropertyValue( monitor, nameof( SqlDatabase.ConnectionString ), "The default connection string.", sourceDescription: "By configurator." );
                }
                else if( db.Name == "histo" )
                {
                    o.SetDirectPropertyValue( monitor, nameof( SqlDatabase.ConnectionString ), "The histo connection string.", sourceDescription: "By configurator." );
                    o.SetDirectPropertyValue( monitor, nameof( SqlDatabase.HasCKCore ), true, sourceDescription: "By configurator." );
                    o.SetDirectPropertyValue( monitor, nameof( SqlDatabase.UseSnapshotIsolation ), true, sourceDescription: "By configurator." );
                }
                else
                {
                    monitor.Warn( $"Unable to find configuration for Database named '{db.Name}' of type {db.GetType()}. Its ConnectionString will be null." );
                }
            }

            static void ConfigureByStObjConstruct( IActivityMonitor monitor, IStObjMutableItem o, SqlDatabase db )
            {
                var fromAbove = o.ConstructParametersAboveRoot;
                Debug.Assert( fromAbove != null, "Since we are on the root of the specializations path." );
                fromAbove.Should().NotBeEmpty().And.HaveCount( 1, "We have only one base class with a StObjConstruct method." );
                var (t, parameters) = fromAbove.Single();
                t.Should().Be( typeof( SqlDatabase ) );

                if( parameters.Count != 3
                    || parameters[0].Name != "connectionString"
                    || parameters[0].Type != typeof( string )
                    || parameters[1].Name != "hasCKCore"
                    || parameters[1].Type != typeof( bool )
                    || parameters[2].Name != "useSnapshotIsolation"
                    || parameters[2].Type != typeof( bool ) )
                {
                    throw new CKException( "Expected SqlDatabase.StObjContruct(string connectionString, bool hasCKCore, bool useSnapshotIsolation)" );
                }
                if( db.IsDefaultDatabase )
                {
                    parameters[0].SetParameterValue( "The default connection string." );
                }
                else if( db.Name == "histo" )
                {
                    parameters[0].SetParameterValue( "The histo connection string." );
                    parameters[1].SetParameterValue( true );
                    parameters[2].SetParameterValue( true );
                }
                else
                {
                    monitor.Warn( $"Unable to find configuration for Database named '{db.Name}' of type {db.GetType()}. Its ConnectionString will be null." );
                }
            }
        }



        [TestCase("UseStObjConstruct")]
        [TestCase( "UseSetDirectProperty" )]
        public void LayeredArchitecture_and_SqlDatabase_configurations( string mode )
        {
            var configurator = new ConfiguratorByStObjConstruct( mode );
            StObjCollector collector = new StObjCollector( new SimpleServiceContainer(), configurator: configurator );
            collector.RegisterTypes( TestHelper.Monitor, new[] { typeof( BasicPackage ),
                                                                 typeof( BasicActor ),
                                                                 typeof( BasicUser ),
                                                                 typeof( BasicGroup ),
                                                                 typeof( ZonePackage ),
                                                                 typeof( ZoneGroup ),
                                                                 typeof( SecurityZone ),
                                                                 typeof( AuthenticationPackage ),
                                                                 typeof( AuthenticationUser ),
                                                                 typeof( AuthenticationDetail ),
                                                                 typeof( SqlDefaultDatabase ),
                                                                 typeof( SqlHistoDatabase ),
                                                                 typeof( SqlAlienDatabase ) } );

            collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
            collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );

            var map = TestHelper.GetSuccessfulResult( collector ).EngineMap;
            Debug.Assert( map != null, "No initialization error." );
            CheckChildren<BasicPackage>( map.StObjs, "BasicActor,BasicUser,BasicGroup" );
            CheckChildren<ZonePackage>( map.StObjs, "SecurityZone,ZoneGroup" );
            CheckChildren<SqlDefaultDatabase>( map.StObjs, "BasicPackage,BasicActor,BasicUser,BasicGroup,ZonePackage,SecurityZone,ZoneGroup,AuthenticationPackage,AuthenticationUser,AuthenticationDetail" );

            var basicPackage = map.StObjs.Obtain<BasicPackage>();
            Debug.Assert( basicPackage != null );
            basicPackage.Should().BeAssignableTo<ZonePackage>();
            basicPackage.GroupHome.Should().BeAssignableTo<ZoneGroup>();
            basicPackage.Schema.Should().Be( "CK" );

            var authenticationUser = map.StObjs.Obtain<AuthenticationUser>();
            Debug.Assert( authenticationUser != null );
            authenticationUser.Schema.Should().Be( "CK" );
            
            var authenticationDetail = map.StObjs.Obtain<AuthenticationDetail>();
            Debug.Assert( authenticationDetail != null );
            authenticationDetail.Schema.Should().Be( "CKAuth" );

            var db = map.StObjs.Obtain<SqlDefaultDatabase>();
            Debug.Assert( db != null );
            db.ConnectionString.Should().Be( "The default connection string." );

            var histo = map.StObjs.Obtain<SqlHistoDatabase>();
            Debug.Assert( histo != null );
            histo.ConnectionString.Should().Be( "The histo connection string." );

            var alien = map.StObjs.Obtain<SqlAlienDatabase>();
            Debug.Assert( alien != null );
            alien.ConnectionString.Should().BeNull();
        }
    }
}
