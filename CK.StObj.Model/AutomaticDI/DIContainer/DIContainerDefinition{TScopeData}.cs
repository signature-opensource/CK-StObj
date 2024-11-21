using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Core;

/// <summary>
/// Base class for a container definition.
/// The specialized class must be decorated with <see cref="DIContainerDefinitionAttribute"/>.
/// The <typeparamref name="TScopeData"/> is a key as well as the <see cref="DIContainerDefinition.Name"/>: all containers
/// must have different name and different scope data type otherwise a setup error will be raised.
/// </summary>
/// <typeparam name="TScopeData">
/// Type of the scoped data that is injected in <see cref="IDIContainerServiceProvider{TScopeData}"/>.
/// Must be a nested <c>public sealed class Data : IScopedData</c> or <c>public sealed class Data : BackendScopedData</c> for
/// <see cref="DIContainerKind.Background"/> containers.
/// </typeparam>
[CKTypeDefiner]
public abstract class DIContainerDefinition<TScopeData> : DIContainerDefinition
    where TScopeData : class, DIContainerDefinition.IScopedData
{
    /// <summary>
    /// Must be implemented to configure the endpoint services.
    /// Resolutions can rely on <paramref name="scopeData"/>, for instance:
    /// <code>
    /// services.AddScoped&lt;IMySpecificService&gt;( sp => scopeData( sp ).MySpecificService );
    /// </code>
    /// Note that if the endpoint doesn't get a scoped IActivityMonitor from the scoped data, the right registration is:
    /// <code>
    /// services.AddScoped&lt;IActivityMonitor,ActivityMonitor&gt;();
    /// services.AddScoped( sp => sp.GetRequiredService&lt;IActivityMonitor&gt;().ParallelLogger );
    /// </code>
    /// If the monitor is provided, this becomes:
    /// <code>
    /// services.AddScoped&lt;IActivityMonitor&gt;( sp => scopeData( sp ).Monitor );
    /// services.AddScoped( sp => sp.GetRequiredService&lt;IActivityMonitor&gt;().ParallelLogger );
    /// </code>
    /// </summary>
    /// <param name="services">Container to configure.</param>
    /// <param name="scopeData">Accessor to the current scoped data.</param>
    /// <param name="globalServiceExists">Provides a way to detect if a service is available.</param>
    public abstract void ConfigureContainerServices( IServiceCollection services,
                                                    Func<IServiceProvider, TScopeData> scopeData,
                                                    IServiceProviderIsService globalServiceExists );
}
