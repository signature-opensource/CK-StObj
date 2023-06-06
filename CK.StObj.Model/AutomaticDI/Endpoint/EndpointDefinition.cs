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

        internal EndpointDefinition() { }

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
            /// </summary>
            protected ScopedData( EndpointUbiquitousInfo ubiquitousInfo )
            {
                Throw.CheckNotNullArgument( ubiquitousInfo );
                UbiquitousEndpointInfoMarshaller = new Dictionary<Type, Func<IServiceProvider, object>>();
                _ubiquitousInfo = ubiquitousInfo;
            }

            public IReadOnlyDictionary<Type, Func<IServiceProvider, object>> UbiquitousEndpointInfoMarshaller { get; }

            /// <summary>
            /// Gets the ubiquitous information.
            /// </summary>
            public EndpointUbiquitousInfo UbiquitousInfo => _ubiquitousInfo;
        }


    }

}
