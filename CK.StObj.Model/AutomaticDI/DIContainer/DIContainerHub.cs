// Ignore Spelling: Deconstruct

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace CK.Core
{
    /// <summary>
    /// Gives access to all the existing <see cref="DIContainerDefinition"/> and <see cref="IDIContainer"/>.
    /// This is a processwide singleton service.
    /// </summary>
    [Setup.ContextBoundDelegation( "CK.Setup.DIContainerHubImpl, CK.StObj.Engine" )]
    public abstract class DIContainerHub : ISingletonAutoService
    {
        /// <summary>
        /// Gets all the container definitions.
        /// </summary>
        public abstract IReadOnlyList<DIContainerDefinition> ContainerDefinitions { get; }

        /// <summary>
        /// Gets all the service types that are declared as endpoint services and their kind.
        /// </summary>
        public abstract IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets all the containers.
        /// </summary>
        public abstract IReadOnlyList<IDIContainer> Containers { get; }

        /// <summary>
        /// Lists all the ambient service types where <see cref="IAutoService"/> inheritance chains
        /// are expanded. See <see cref="AmbientServiceMapping"/>. Order matters: consecutive entries with
        /// the same <see cref="AmbientServiceMapping.MappingIndex"/> belong to the same auto service inheritance
        /// chain.
        /// </summary>
        public abstract IReadOnlyList<AmbientServiceMapping> AmbientServiceMappings { get; }

        /// <summary>
        /// A AmbientServiceMapping supports auto service unique mappings by associating
        /// to ambient auto service a single entry: <see cref="AmbientServiceHub"/> uses this
        /// when overriding a value to automatically sets all the unique mappings to the same value whatever
        /// the type used as the key.
        /// <para>
        /// This is also used to map to the <see cref="IAmbientServiceDefaultProvider{T}"/> that must be used when a
        /// ambient service resolution is not registered by a endpoint.
        /// </para>
        /// </summary>
        public readonly struct AmbientServiceMapping
        {
            /// <summary>
            /// Initializes a new mapping.
            /// </summary>
            /// <param name="ambientServiceType">The ambient service type.</param>
            /// <param name="mappingIndex">The index.</param>
            public AmbientServiceMapping( Type ambientServiceType, int mappingIndex )
            {
                AmbientServiceType = ambientServiceType;
                MappingIndex = mappingIndex;
            }

            /// <summary>
            /// The ubiquitous type.
            /// </summary>
            public Type AmbientServiceType { get; }

            /// <summary>
            /// The mapping index. The same index is used for all unique mappings
            /// to the same most specialized type.
            /// </summary>
            public int MappingIndex { get; }


#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
            [EditorBrowsable(EditorBrowsableState.Never)]
            public void Deconstruct( out Type t, out int i )
            {
                t = AmbientServiceType;
                i = MappingIndex;
            }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
        }

    }

}
