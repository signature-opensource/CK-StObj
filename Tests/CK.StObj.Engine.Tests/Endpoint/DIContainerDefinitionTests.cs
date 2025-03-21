
using CK.Core;
using CK.Testing;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.StObj.Engine.Tests.Endpoint;

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
    public async Task DIContainerHub_exposes_the_DIContainerDefinitions_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( AppIdentityDIContainerDefinition ), typeof( BackdoorDIContainerDefinition ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

        var manager = auto.Services.GetRequiredService<DIContainerHub>();
        manager.ContainerDefinitions.Count.ShouldBe( 2 );
        manager.ContainerDefinitions.ShouldContain( e => e is AppIdentityDIContainerDefinition );
        manager.ContainerDefinitions.ShouldContain( e => e is BackdoorDIContainerDefinition );
    }

    [Test]
    public async Task EndpointTypes_are_available_in_containers_as_well_as_the_IEnumerable_of_IEndpoint_Async()
    {
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Types.Add( typeof( AppIdentityDIContainerDefinition ), typeof( BackdoorDIContainerDefinition ) );
        await using var auto = (await configuration.RunAsync().ConfigureAwait( false )).CreateAutomaticServices();

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

        o1.SequenceEqual( o2 ).ShouldBeTrue();
        o2.SequenceEqual( o3 ).ShouldBeTrue();
        o3.SequenceEqual( o4 ).ShouldBeTrue();
        o4.SequenceEqual( o5 ).ShouldBeTrue();
        o5.SequenceEqual( o6 ).ShouldBeTrue();
    }

    static object[] GetEndpointsAndOtherTrueSingletons( IServiceProvider s )
    {
        var endpoints = s.GetRequiredService<IEnumerable<IDIContainer>>();
        endpoints.Count().ShouldBe( 2 );
        var appIdentity = s.GetRequiredService<IDIContainer<AppIdentityDIContainerDefinition.Data>>();
        appIdentity.Name.ShouldBe( "AppIdentity" );
        var backdoor = s.GetRequiredService<IDIContainer<BackdoorDIContainerDefinition.Data>>();
        backdoor.Name.ShouldBe( "Backdoor" );
        endpoints.ShouldContain( appIdentity );
        endpoints.ShouldContain( backdoor );
        return [endpoints, appIdentity, backdoor, s.GetRequiredService<DIContainerHub>(), s.GetRequiredService<IStObjMap>()];
    }

    [DIContainerDefinition( DIContainerKind.Background )]
    public abstract class NoWay1Definition : BackdoorDIContainerDefinition
    {
    }

    [Test]
    public void DIContainerDefinitions_cannot_be_specialized()
    {
        TestHelper.GetFailedCollectorResult( [typeof( NoWay1Definition )],
                "DIContainerDefinition type 'DIContainerDefinitionTests.NoWay1Definition' must directly specialize "
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
        TestHelper.GetFailedCollectorResult( [typeof( Dup1DIContainerDefinition ), typeof( Dup2DIContainerDefinition )],
            "The generic parameter of 'DIContainerDefinitionTests.Dup2DIContainerDefinition' must be 'Dup2DIContainerDefinition.Data'." );
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

        TestHelper.GetFailedCollectorResult( [typeof( BadNameDefinition )], msg );
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

            TestHelper.GetFailedCollectorResult( [typeof( BadFrontDataDIContainerDefinition )], msg );
        }

        {
            const string msg = "Type 'DIContainerDefinitionTests.BadBackDataDIContainerDefinition.Data' must specialize BackendScopedData because it is a Backend DI container.";

            TestHelper.GetFailedCollectorResult( [typeof( BadBackDataDIContainerDefinition )], msg );
        }
    }


}
