using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Defines Auto services flags.
    /// </summary>
    [Flags]
    public enum AutoServiceKind
    {
        /// <summary>
        /// Not a service we handle or external service for which
        /// no lifetime nor any information is known.
        /// </summary>
        None = 0,

        /// <summary>
        /// This is a service bound to a endpoint: it may not be available in endpoint (a typical example of such service is the
        /// IAuthenticationService that requires an HttpContext) and may not be available in the global DI container (a service
        /// can be specific to a given endpoint or a family of endpoints but cannot live in the global application scope).
        /// </summary>
        IsEndpointService = 2,

        /// <summary>
        /// This service must be registered as a Singleton.
        /// </summary>
        IsSingleton = 8,

        /// <summary>
        /// This service must be registered as a Scoped service.
        /// </summary>
        IsScoped = 16,

        /// <summary>
        /// This is applicable only to interfaces. It states that the service is not unique: interfaces marked with this flag must all
        /// be registered, associated to each of their implementation.
        /// </summary>
        IsMultipleService = 32,

        /// <summary>
        /// Auto service flag. This flag is set if and only if the type is marked with a <see cref="IAutoService"/> interface marker.
        /// </summary>
        IsAutoService = 64,

        /// <summary>
        /// Ubiquitous info is a scoped endpoint service (and optionally a auto service) that must be available in all
        /// containers. The instance must be directly marshallable (should be immutable or at least thread safe and
        /// be independent of any other service). See <see cref="EndpointScopedServiceAttribute"/>.
        /// </summary>
        UbiquitousInfo = 128 | IsEndpointService | IsScoped
    }
}
