using System;
using System.Collections.Generic;
using static CK.Core.EndpointDefinition;

namespace CK.Core
{

    /// <summary>
    /// Non generic endpoint definition.
    /// <see cref="EndpointDefinition{TScopeData}"/> must be used as the base class
    /// for endpoint definition.
    /// </summary>
    [CKTypeSuperDefiner]
    public abstract class EndpointDefinition : IRealObject
    {
        /// <summary>
        /// Gets this endpoint name that must be unique.
        /// This is automatically implemented as the prefix of the implementing type name:
        /// "XXX" for "XXXEndpointDefinition".
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets this endpoint kind (from <see cref="EndpointDefinitionAttribute"/>).
        /// This is automatically implemented.
        /// </summary>
        public abstract EndpointKind Kind { get; }

        // The only allowed specialization is EndpointDefinition<TScopeData>
        internal EndpointDefinition()
        {
        }

        /// <summary>
        /// Base endpoint scoped data that enables ubiquitous scoped service informations
        /// marshalling: this must be specialized for each endpoint definition: the specialized
        /// ScopedData type is the key to resolve the <see cref="IEndpointType{TScopeData}"/> that
        /// exposes the final DI container.
        /// </summary>
        public class ScopedData
        {
            readonly EndpointUbiquitousInfo _ubiquitousInfo;

            /// <summary>
            /// It is required to provide the endpoint definition instance here so that
            /// the ubiquitous marshaller can be configured with the existing ubiquitous
            /// endpoint services.
            /// <para>
            /// Extra parameters can be freely defined (typically the <see cref="IActivityMonitor"/> that must be used in the scope),
            /// including ones that are ubiquitous information services: this is the explicit and type safe way to inject ubiquitous
            /// informations that is both more explicit and efficient that using <see cref="EndpointUbiquitousInfo.Override{T}(T)"/>
            /// methods.
            /// </para>
            /// </summary>
            protected ScopedData( EndpointUbiquitousInfo ubiquitousInfo )
            {
                Throw.CheckNotNullArgument( ubiquitousInfo );
                _ubiquitousInfo = ubiquitousInfo;
            }

            /// <summary>
            /// Gets the ubiquitous information.
            /// </summary>
            public EndpointUbiquitousInfo UbiquitousInfo => _ubiquitousInfo;
        }


    }

}
