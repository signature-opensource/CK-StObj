

using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Testing.StObjEngine
{

    /// <summary>
    /// Captures the result of <see cref="IStObjEngineTestHelperCore.CreateAutomaticServices(StObjCollector, Func{StObjEngineConfiguration, StObjEngineConfiguration}?, SimpleServiceContainer?, Action{StObjContextRoot.ServiceRegister}?)"/>.
    /// </summary>
    public sealed class AutomaticServicesResult
    {
        /// <summary>
        /// Gets the result of the code generation and load.
        /// </summary>
        public CompileAndLoadResult CompileAndLoadResult { get; }

        /// <summary>
        /// Gets the result of the type analysis (the <see cref="CompileAndLoadResult.CollectorResult"/>.
        /// </summary>
        public StObjCollectorResult CollectorResult => CompileAndLoadResult.CollectorResult;

        /// <summary>
        /// Gets the StObjMap (the <see cref="CompileAndLoadResult.Map"/>).
        /// </summary>
        public IStObjMap Map => CompileAndLoadResult.Map;

        /// <summary>
        /// Gets the <see cref="StObjContextRoot.ServiceRegister"/> that has been used to
        /// configure the <see cref="Services"/> and can be used to configure and build
        /// other <see cref="ServiceProvider"/>.
        /// </summary>
        public readonly StObjContextRoot.ServiceRegister ServiceRegister;

        /// <summary>
        /// Gets a configured service provider. This MUST be disposed once done with it.
        /// </summary>
        public readonly ServiceProvider Services;

        internal AutomaticServicesResult( CompileAndLoadResult r, StObjContextRoot.ServiceRegister serviceRegister, ServiceProvider services )
        {
            CompileAndLoadResult = r;
            ServiceRegister = serviceRegister;
            Services = services;
        }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        public void Deconstruct( out StObjCollectorResult collector, out IStObjMap map, out StObjContextRoot.ServiceRegister serviceRegister, out ServiceProvider services )
        {
            collector = CollectorResult;
            map = Map;
            serviceRegister = ServiceRegister;
            services = Services;
        }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
