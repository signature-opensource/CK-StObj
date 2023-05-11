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
        /// Indicates a service that has a data/configuration adherence to the current process: it requires some
        /// sort of marshalling/configuration to be able to do its job remotely (out of this process).
        /// (A typical example is the IOptions&lt;&gt; implementations for instance.) 
        /// </summary>
        IsProcessService = 1,

        /// <summary>
        /// This is a service bound to a End Point: even inside this process, it may not be available
        /// (a typical example of such service is the IAuthenticationService that requires an HttpContext).
        /// This flag implies <see cref="IsProcessService"/>.
        /// </summary>
        IsEndpointService = 2,

        /// <summary>
        /// This service is marshallable. This is independent of <see cref="IsProcessService"/> and <see cref="IsEndpointService"/>.
        /// </summary>
        IsMarshallable = 4,

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
        IsMultipleService = 32
    }
}
