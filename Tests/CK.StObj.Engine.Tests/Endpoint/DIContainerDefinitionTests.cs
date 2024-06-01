
using CK.Core;
using CK.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.StObjEngineTestHelper;
// Ignore Spelling: App Backdoor

namespace CK.StObj.Engine.Tests.Endpoint
{
    [TestFixture]
    public class DIContainerDefinitionTests
    {
        [DIContainerDefinition( DIContainerKind.Endpoint )]
        public abstract class AppIdentityDIContainerDefinition : DIContainerDefinition<AppIdentityDIContainerDefinition.Data>
        {
            public sealed class Data : IScopedData
            {
            }

            public override void ConfigureContainerServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [DIContainerDefinition( DIContainerKind.Endpoint )]
        public abstract class BackdoorDIContainerDefinition : DIContainerDefinition<BackdoorDIContainerDefinition.Data>
        {
            public sealed class Data : IScopedData
            {
            }

            public override void ConfigureContainerServices( IServiceCollection services,
                                                            Func<IServiceProvider, Data> scopeData,
                                                            IServiceProviderIsService globalServiceExists )
            {
                services.AddScoped<IActivityMonitor, ActivityMonitor>();
                services.AddScoped( sp => sp.GetRequiredService<IActivityMonitor>().ParallelLogger );
            }
        }

        [Test]
        public void DIContainerHub_exposes_the_DIContainerDefinitions()
        {
            var c = TestHelper.CreateTypeCollector(typeof(AppIdentityDIContainerDefinition), typeof(BackdoorDIContainerDefinition));
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );

            var manager = auto.Services.GetRequiredService<DIContainerHub>();
            manager.ContainerDefinitions.Should().HaveCount( 2 );
            manager.ContainerDefinitions.Should().Contain( e => e is AppIdentityDIContainerDefinition )
                                                .And.Contain( e => e is BackdoorDIContainerDefinition );
        }

        [Test]
        public void EndpointTypes_are_available_in_containers_as_well_as_the_IEnumerable_of_IEndpoint()
        {
            var c = TestHelper.CreateTypeCollector( typeof( AppIdentityDIContainerDefinition ), typeof( BackdoorDIContainerDefinition ) );
            using var auto = TestHelper.CreateSingleBinPathAutomaticServices( c );

            // From the root (singleton) container.
            var o1 = GetEndpointsAndOtherTrueSingletons( auto.Services );
            var backdoor = auto.Services.GetRequiredService<IDIContainer<BackdoorDIContainerDefinition.Data>>();
            var appIdentity = auto.Services.GetRequiredService<IDIContainer<AppIdentityDIContainerDefinition.Data>>();

            using var sScope = auto.Services.CreateScope();
            var o2 = GetEndpointsAndOtherTrueSingletons( sScope.ServiceProvider );

            var sB = backdoor.GetContainer();
            var sA = appIdentity.GetContainer();

            var o3 = GetEndpointsAndOtherTrueSingletons( sB );
            var o4 = GetEndpointsAndOtherTrueSingletons( sA );

            using var sScopeA = sA.CreateScope();
            using var sScopeB = sB.CreateScope();

            var o5 = GetEndpointsAndOtherTrueSingletons( sScopeA.ServiceProvider );
            var o6 = GetEndpointsAndOtherTrueSingletons( sScopeB.ServiceProvider );

            o1.SequenceEqual( o2 ).Should().BeTrue();
            o2.SequenceEqual( o3 ).Should().BeTrue();
            o3.SequenceEqual( o4 ).Should().BeTrue();
            o4.SequenceEqual( o5 ).Should().BeTrue();
            o5.SequenceEqual( o6 ).Should().BeTrue();
        }

        static object[] GetEndpointsAndOtherTrueSingletons( IServiceProvider s )
        {
            var endpoints = s.GetRequiredService<IEnumerable<IDIContainer>>();
            endpoints.Should().HaveCount( 2 );
            var appIdentity = s.GetRequiredService<IDIContainer<AppIdentityDIContainerDefinition.Data>>();
            appIdentity.Name.Should().Be( "AppIdentity" );
            var backdoor = s.GetRequiredService<IDIContainer<BackdoorDIContainerDefinition.Data>>();
            backdoor.Name.Should().Be( "Backdoor" );
            endpoints.Should().Contain( appIdentity ).And.Contain( backdoor );
            return new object[] { endpoints, appIdentity, backdoor, s.GetRequiredService<DIContainerHub>(), s.GetRequiredService<IStObjMap>() }; 
        }

        [DIContainerDefinition( DIContainerKind.Background )]
        public abstract class NoWay1Definition : BackdoorDIContainerDefinition
        {
        }

        [Test]
        public void DIContainerDefinitions_cannot_be_specialized()
        {
            var c1 = TestHelper.CreateTypeCollector( typeof( NoWay1Definition ) );
            TestHelper.GetFailedCollectorResult( c1 , "DIContainerDefinition type 'DIContainerDefinitionTests.NoWay1Definition' must directly specialize "
                                                      + "DIContainerDefinition<TScopeData> (not 'DIContainerDefinitionTests.BackdoorDIContainerDefinition')." );

        }

        [DIContainerDefinition( DIContainerKind.Background )]
        public abstract class Dup1DIContainerDefinition : DIContainerDefinition<Dup1DIContainerDefinition.Data>
        {
            public sealed class Data : BackendScopedData
            {
                public Data( AmbientServiceHub ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }
        }

        [DIContainerDefinition( DIContainerKind.Background )]
        public abstract class Dup2DIContainerDefinition : DIContainerDefinition<Dup1DIContainerDefinition.Data>
        {
            public sealed class Data : BackendScopedData
            {
                public Data( AmbientServiceHub ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }
        }

        [Test]
        public void DIContainerDefinitions_cannot_use_the_same_ScopeData_type()
        {
            var c1 = TestHelper.CreateTypeCollector( typeof( Dup1DIContainerDefinition ), typeof( Dup2DIContainerDefinition ) );
            TestHelper.GetFailedCollectorResult( c1, "The generic parameter of 'DIContainerDefinitionTests.Dup2DIContainerDefinition' must be 'Dup2DIContainerDefinition.Data'." );
        }

        [DIContainerDefinition( DIContainerKind.Endpoint )]
        public abstract class BadNameDefinition : DIContainerDefinition<BadNameDefinition.Data>
        {
            public sealed class Data : IScopedData
            {
            }

        }

        [Test]
        public void DIContainerDefinition_type_name_matters()
        {
            const string msg = "Invalid DIContainerDefinition type 'DIContainerDefinitionTests.BadNameDefinition': "
                               + "DIContainerDefinition type name must end with \"DIContainerDefinition\" (the prefix becomes the container name).";

            var c1 = TestHelper.CreateTypeCollector( typeof( BadNameDefinition ) );
            TestHelper.GetFailedCollectorResult( c1, msg );
        }

        [DIContainerDefinition( DIContainerKind.Endpoint )]
        public abstract class BadFrontDataDIContainerDefinition : DIContainerDefinition<BadFrontDataDIContainerDefinition.Data>
        {
            public sealed class Data : BackendScopedData
            {
                public Data( AmbientServiceHub ubiquitousInfo )
                    : base( ubiquitousInfo )
                {
                }
            }

        }

        [DIContainerDefinition( DIContainerKind.Background )]
        public abstract class BadBackDataDIContainerDefinition : DIContainerDefinition<BadBackDataDIContainerDefinition.Data>
        {
            public sealed class Data : IScopedData
            {
            }
        }

        [Test]
        public void DIContainerDefinition_Data_type_is_checked()
        {
            {
                const string msg = "Type 'DIContainerDefinitionTests.BadFrontDataDIContainerDefinition.Data' must not specialize BackendScopedData, " +
                                    "it must simply support the IScopedData interface because it is a Endpoint DI container.";

                var c = TestHelper.CreateTypeCollector( typeof( BadFrontDataDIContainerDefinition ) );
                TestHelper.GetFailedCollectorResult( c, msg );
            }

            {
                const string msg = "Type 'DIContainerDefinitionTests.BadBackDataDIContainerDefinition.Data' must specialize BackendScopedData because it is a Backend DI container.";

                var c = TestHelper.CreateTypeCollector( typeof( BadBackDataDIContainerDefinition ) );
                TestHelper.GetFailedCollectorResult( c, msg );
            }
        }


    }
}
