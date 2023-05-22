using CK.Setup;
using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Base class for a endpoint.
    /// The specialized class must be decorated with <see cref="EndpointDefinitionAttribute"/>.
    /// </summary>
    [CKTypeDefiner]
    public abstract class EndpointDefinition : IRealObject
    {
        /// <summary>
        /// Gets this endpoint name.
        /// This is automatically implemented.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the scoped service types handled by this endpoint.
        /// This is automatically implemented.
        /// </summary>
        public abstract IReadOnlyList<Type> ScopedServices { get; }

        /// <summary>
        /// Gets the singleton service types exposed by this endpoint.
        /// This is automatically implemented.
        /// </summary>
        public abstract IReadOnlyList<Type> SingletonServices { get; }

    }

}
