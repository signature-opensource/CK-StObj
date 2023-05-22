using CK.Setup;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System;

namespace CK.Core
{
    /// <summary>
    /// The default endpoint type models the global dependency container
    /// built by the application host.
    /// It can be associated to services that cannot or must not be available
    /// in other endpoints.
    /// </summary>
    [ContextBoundDelegation( "CK.Setup.DefaultEndpointDefinitionImpl, CK.StObj.Engine" )]
    public abstract class DefaultEndpointDefinition : EndpointDefinition
    {
        /// <summary>
        /// Gets "Default".
        /// </summary>
        public override string Name => "Default";

    }

}
