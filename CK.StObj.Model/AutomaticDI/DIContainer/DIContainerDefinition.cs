namespace CK.Core
{

    /// <summary>
    /// Non generic DI container definition.
    /// <see cref="DIContainerDefinition{TScopeData}"/> must be used as the base class
    /// for container definition.
    /// </summary>
    [CKTypeSuperDefiner]
    public abstract class DIContainerDefinition : IRealObject
    {
        /// <summary>
        /// Gets this container name that must be unique.
        /// This is automatically implemented as the prefix of the implementing type name:
        /// "XXX" for "XXXDIContainerDefinition".
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets this container kind (from <see cref="DIContainerDefinitionAttribute"/>).
        /// This is automatically implemented.
        /// </summary>
        public abstract DIContainerKind Kind { get; }

        // The only allowed specialization is DIContainerDefinition<TScopeData>
        internal DIContainerDefinition()
        {
        }

        /// <summary>
        /// Marker interface for scope data. It is enough for <see cref="DIContainerKind.Endpoint"/> endpoints to
        /// simply support it, but background containers must specialize <see cref="BackendScopedData"/>.
        /// <para>
        /// A nested <c>public sealed class Data : IScopedData</c> (or <c>public sealed class Data : BackendScopedData</c> for
        /// backgrond containers) must be defined: this nested <c>Data</c> type is the key to resolve
        /// the <see cref="IDIContainer{TScopeData}"/> that exposes the final DI container.
        /// </para>
        /// </summary>
        public interface IScopedData
        {
        }

        /// <summary>
        /// Required base endpoint scoped data for <see cref="DIContainerKind.Background"/> endpoints.
        /// This enables ambient service informations marshalling from the calling context
        /// to the called context.
        /// </summary>
        public class BackendScopedData : IScopedData
        {
            AmbientServiceHub? _ambientServiceHub;

            /// <summary>
            /// It is required to provide the endpoint definition instance here so that
            /// the ambient services marshaller can be configured with the existing ambient
            /// endpoint services.
            /// <para>
            /// Extra parameters (and their fields) can be freely defined (typically the <see cref="IActivityMonitor"/> that must
            /// be used in the scope), including ones that are ambient services: this is an explicit and type safe way to inject
            /// ambient informations that is both more explicit and efficient than using <see cref="AmbientServiceHub.Override{T}(T)"/>
            /// methods.
            /// </para>
            /// </summary>
            /// <param name="ambientServiceHub">
            /// An existing capture of the Ambient services that must be used or null if this can (or should be)
            /// computed at the latest possible moment, before <see cref="IDIContainerServiceProvider{TScopeData}.CreateScope(TScopeData)"/> or
            /// <see cref="IDIContainerServiceProvider{TScopeData}.CreateAsyncScope(TScopeData)"/> is called.
            /// </param>
            protected BackendScopedData( AmbientServiceHub? ambientServiceHub )
            {
                _ambientServiceHub = ambientServiceHub;
            }

            /// <summary>
            /// Gets or sets the AmbientServiceHub.
            /// <para>
            /// This must be not null when calling <see cref="IDIContainerServiceProvider{TScopeData}.CreateScope(TScopeData)"/> or
            /// <see cref="IDIContainerServiceProvider{TScopeData}.CreateAsyncScope(TScopeData)"/>.
            /// </para>
            /// </summary>
            public AmbientServiceHub? AmbientServiceHub
            {
                get => _ambientServiceHub;
                set => _ambientServiceHub = value;
            }
        }

    }

}
