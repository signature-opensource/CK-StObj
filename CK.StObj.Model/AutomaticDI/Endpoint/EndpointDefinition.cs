using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Non generic endpoint definition.
    /// <see cref="EndpointDefinition{TInstanceData}"/> must be used as the base class
    /// for endpoint definition.
    /// </summary>
    /// <remarks>
    /// This cannot use a [<see cref="CKTypeSuperDefinerAttribute"/>] because <see cref="DefaultEndpointDefinition"/>
    /// directly specializes this.
    /// </remarks>
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
