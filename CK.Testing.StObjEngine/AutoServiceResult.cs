

using CK.Core;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace CK.Testing.StObjEngine
{

    /// <summary>
    /// Captures the result of <see cref="IStObjEngineTestHelperCore.CreateAutomaticServices(StObjCollector, Action{StObjContextRoot.ServiceRegister}?, SimpleServiceContainer?)"/>.
    /// </summary>
    public readonly struct AutoServiceResult
    {
        /// <summary>
        /// Gets the result of the type analysis.
        /// </summary>
        public readonly StObjCollectorResult Result;

        /// <summary>
        /// Gets the static StObjMap.
        /// </summary>
        public readonly IStObjMap Map;

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

        public AutoServiceResult( StObjCollectorResult result, IStObjMap map, StObjContextRoot.ServiceRegister serviceRegister, ServiceProvider services )
        {
            Result = result;
            Map = map;
            ServiceRegister = serviceRegister;
            Services = services;
        }

        public void Deconstruct( out StObjCollectorResult result, out IStObjMap map, out StObjContextRoot.ServiceRegister serviceRegister, out ServiceProvider services )
        {
            result = Result;
            map = Map;
            serviceRegister = ServiceRegister;
            services = Services;
        }

        public static implicit operator (StObjCollectorResult Result, IStObjMap Map, StObjContextRoot.ServiceRegister ServiceRegistrar, ServiceProvider Services)( AutoServiceResult value )
        {
            return (value.Result, value.Map, value.ServiceRegister, value.Services);
        }

        public static implicit operator AutoServiceResult( (StObjCollectorResult Result, IStObjMap Map, StObjContextRoot.ServiceRegister ServiceRegistrar, ServiceProvider Services) value )
        {
            return new AutoServiceResult( value.Result, value.Map, value.ServiceRegistrar, value.Services );
        }
    }
}
