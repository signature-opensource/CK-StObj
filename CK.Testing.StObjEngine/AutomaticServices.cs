using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Testing;


/// <summary>
/// Captures the result of <see cref="EngineTestHelperExtensions.RunSingleBinPathAndLoad(IMonitorTestHelper, System.Collections.Generic.ISet{Type})"/>.
/// </summary>
public readonly struct AutomaticServices : IDisposable
{
    readonly IStObjMap _map;
    readonly ServiceProvider _serviceProvider;
    readonly IServiceCollection _serviceCollection;

    internal AutomaticServices( IStObjMap map, ServiceProvider serviceProvider, IServiceCollection serviceCollection )
    {
        _map = map;
        _serviceProvider = serviceProvider;
        _serviceCollection = serviceCollection;
    }

    /// <summary>
    /// Gets the CKomposable map.
    /// </summary>
    public IStObjMap Map => _map;

    /// <summary>
    /// Gets the configured services.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Gets the service collection that has been used to build the <see cref="Services"/>.
    /// </summary>
    public IServiceCollection ServiceCollection => _serviceCollection;

    /// <summary>
    /// Disposes the encapsulated <see cref="ServiceProvider"/>.
    /// </summary>
    public void Dispose()
    {
        _serviceProvider.Dispose();
    }
}
