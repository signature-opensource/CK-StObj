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
        /// Marker interface for scope data. It is enough for <see cref="EndpointKind.Front"/> endpoints to
        /// simply support it, but back endpoints must specialize <see cref="BackScopedData"/>.
        /// <para>
        /// A nested <c>public sealed class Data : IScopedData</c> (or <c>public sealed class Data : BackScopedData</c> for
        /// back endpoints) must be defined for each endpoint definition: this nested <c>Data</c> type is the key to resolve
        /// the <see cref="IEndpointType{TScopeData}"/> that exposes the final DI container.
        /// </para>
        /// </summary>
        public interface IScopedData
        {
        }

        /// <summary>
        /// Required base endpoint scoped data for <see cref="EndpointKind.Back"/> endpoints.
        /// This enables ambient service informations marshalling from the calling context
        /// to the called context.
        /// </summary>
        public class BackScopedData : IScopedData
        {
            readonly AmbientServiceHub _ambientServiceHub;

            /// <summary>
            /// It is required to provide the endpoint definition instance here so that
            /// the ambient services marshaller can be configured with the existing ambient
            /// endpoint services.
            /// <para>
            /// Extra parameters can be freely defined (typically the <see cref="IActivityMonitor"/> that must be used in the scope),
            /// including ones that are ambient services: this is the explicit and type safe way to inject ambient
            /// informations that is both more explicit and efficient than using <see cref="AmbientServiceHub.Override{T}(T)"/>
            /// methods.
            /// </para>
            /// </summary>
            protected BackScopedData( AmbientServiceHub ambientServiceHub )
            {
                Throw.CheckNotNullArgument( ambientServiceHub );
                _ambientServiceHub = ambientServiceHub;
            }

            /// <summary>
            /// Gets the AmbientServiceHub.
            /// </summary>
            public AmbientServiceHub AmbientServiceHub => _ambientServiceHub;
        }

    }

}
