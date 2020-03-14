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
        /// no lifetime nor front binding is known.
        /// </summary>
        None = 0,

        /// <summary>
        /// This is a front service, bound to the front process that cannot be used directly in
        /// another process and needs to be marshalled to any other process (a typical example is
        /// the IOptions<> implementations for instance). 
        /// </summary>
        IsFrontProcessService = 1,

        /// <summary>
        /// This is a front service bound to the End Point: even inside the front process, it cannot be used directly
        /// (a typical example of such service is the HttpContext). To be used by a background service, it must be <see cref="IsMarshallable"/>.
        /// This flag implies <see cref="IsFrontProcessService"/> AND <see cref="IsScoped"/>: a Front only service is necessarily Scoped
        /// (since a Singleton is, by design, available in the whole process).
        /// </summary>
        IsFrontService = 2,

        /// <summary>
        /// This service is marchallable. This is independent of <see cref="IsFrontProcessService"/> and <see cref="IsFrontService"/>.
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
