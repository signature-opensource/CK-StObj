using System;
using CK.Core;
using CK.Setup;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.ActorZoneTests
{
    [TestFixture]
    public class WithoutAmbientTests
    {
        [StObj( ItemKind = DependentItemKindSpec.Group,
                Children = new Type[] 
                { 
                    typeof( BasicPackage ), 
                    typeof( BasicActor ), 
                    typeof( BasicUser ), 
                    typeof( BasicGroup ), 
                    typeof( ZonePackage ), 
                    typeof( ZoneGroup ), 
                    typeof( SecurityZone ),
                    typeof( AuthenticationPackage ),
                    typeof( AuthenticationUser )
                } )]

        public class SqlDatabaseDefault : IRealObject
        {
        }

        #region Basic Package

        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class BasicPackage : IRealObject
        {
        }

        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicActor : IRealObject
        {
        }


        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
        public class BasicUser : IRealObject
        {
        }


        [StObj( Container = typeof( BasicPackage ), ItemKind = DependentItemKindSpec.Item )]
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

        [StObj( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public class ZoneGroup : BasicGroup
        {
            void StObjConstruct( ISecurityZone zone )
            {
            }
        }

        public interface ISecurityZone : IRealObject
        {
        }

        [StObj( Container = typeof( ZonePackage ), ItemKind = DependentItemKindSpec.Item )]
        public class SecurityZone : ISecurityZone
        {
            void StObjConstruct( BasicGroup group )
            {
            }
        }

        #endregion

        #region Authentication Package

        [StObj( ItemKind = DependentItemKindSpec.Container )]
        public class AuthenticationPackage : IRealObject
        {
        }

        [StObj( Container = typeof( AuthenticationPackage ) )]
        public class AuthenticationUser : BasicUser
        {
        }

        #endregion 


        [Test]
        public void LayeredArchitecture()
        {
            StObjCollector collector = new StObjCollector( TestHelper.Monitor, new SimpleServiceContainer() );
            collector.RegisterType( typeof( BasicPackage ) );
            collector.RegisterType( typeof( BasicActor ) );
            collector.RegisterType( typeof( BasicUser ) );
            collector.RegisterType( typeof( BasicGroup ) );
            collector.RegisterType( typeof( ZonePackage ) );
            collector.RegisterType( typeof( ZoneGroup ) );
            collector.RegisterType( typeof( SecurityZone ) );
            collector.RegisterType( typeof( AuthenticationPackage ) );
            collector.RegisterType( typeof( AuthenticationUser ) );
            collector.RegisterType( typeof( SqlDatabaseDefault ) );
            collector.DependencySorterHookInput = items => items.Trace( TestHelper.Monitor );
            collector.DependencySorterHookOutput = sortedItems => sortedItems.Trace( TestHelper.Monitor );
            
            var r = collector.GetResult();
            Assert.That( r.HasFatalError, Is.False );

            WithAmbientTests.CheckChildren<BasicPackage>( r.StObjs, "BasicActor,BasicUser,BasicGroup" );
            WithAmbientTests.CheckChildren<ZonePackage>( r.StObjs, "SecurityZone,ZoneGroup" );
            WithAmbientTests.CheckChildren<SqlDatabaseDefault>( r.StObjs, "BasicPackage,BasicActor,BasicUser,BasicGroup,ZonePackage,SecurityZone,ZoneGroup,AuthenticationPackage,AuthenticationUser" );
        }
    }
}
