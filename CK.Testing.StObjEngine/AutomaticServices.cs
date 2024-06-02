using CK.Core;
using CK.Testing.StObjEngine;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CK.Testing
{
    /// <summary>
    /// Captures the result of <see cref="EngineTestHelperExtensions.RunSingleBinPathAndLoad(IMonitorTestHelper, System.Collections.Generic.ISet{Type})"/>.
    /// </summary>
    public readonly struct AutomaticServices : IDisposable
    {
        readonly RunAndLoadResult _loadResult;
        readonly ServiceProvider _serviceProvider;
        readonly StObjContextRoot.ServiceRegister _serviceRegister;

        internal AutomaticServices( RunAndLoadResult r, ServiceProvider serviceProvider, StObjContextRoot.ServiceRegister serviceRegister )
        {
            _loadResult = r;
            _serviceProvider = serviceProvider;
            _serviceRegister = serviceRegister;
        }

        /// <summary>
        /// Gets the map load result.
        /// </summary>
        public RunAndLoadResult LoadResult => _loadResult;

        /// <summary>
        /// Gets the configured services.
        /// </summary>
        public IServiceProvider Services => _serviceProvider;

        /// <summary>
        /// Gets the service register.
        /// </summary>
        public StObjContextRoot.ServiceRegister ServiceRegister => _serviceRegister;

        /// <summary>
        /// Disposes the encapsulated <see cref="ServiceProvider"/>.
        /// </summary>
        public void Dispose()
        {
            _serviceProvider.Dispose();
        }
    }
}
