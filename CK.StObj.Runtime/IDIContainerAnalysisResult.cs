using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Captures the information about all the <see cref="DIContainerDefinition"/>.
    /// </summary>
    public interface IDIContainerAnalysisResult
    {
        /// <summary>
        /// Gets all the <see cref="IDIContainerInfo"/>.
        /// </summary>
        IReadOnlyList<IDIContainerInfo> Containers { get; }

        /// <summary>
        /// Gets all the endpoint service types and their kind (they are not necessarily <see cref="IAutoService"/>).
        /// </summary>
        IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets whether at least one ambient information service type exists.
        /// </summary>
        bool HasAmbientServices { get; }

        /// <summary>
        /// Lists all the ubiquitous service types where <see cref="IAutoService"/> inheritance chains
        /// are expanded. See <see cref="DIContainerHub.AmbientServiceMapping"/>. Order matters: consecutive entries with
        /// the same <see cref="DIContainerHub.AmbientServiceMapping.MappingIndex"/> belong to the same auto service inheritance
        /// chain.
        /// </summary>
        IReadOnlyList<DIContainerHub.AmbientServiceMapping> AmbientServiceMappings { get; }

        /// <summary>
        /// Captures the ambient service default provider implementation.
        /// </summary>
        public readonly struct AmbientServiceDefault
        {
            /// <summary>
            /// Gets the <see cref="IEndpointUbiquitousServiceDefault{T}"/> type that the
            /// <see cref="Provider"/> implements.
            /// </summary>
            public Type ProviderType { get; }

            /// <summary>
            /// Gets the service implementation.
            /// </summary>
            public IStObjFinalClass Provider { get; }

            /// <summary>
            /// Initializes a new default.
            /// </summary>
            /// <param name="providerType">The <see cref="IEndpointUbiquitousServiceDefault{T}"/> type.</param>
            /// <param name="provider">The service implementation.</param>
            public AmbientServiceDefault( Type providerType, IStObjFinalClass provider )
            {
                ProviderType = providerType;
                Provider = provider;
            }
        }

        /// <summary>
        /// Gets the <see cref="IEndpointUbiquitousServiceDefault{T}"/> to use for each mapped ambient
        /// service.
        /// </summary>
        IReadOnlyList<AmbientServiceDefault> DefaultAmbientServiceValueProviders { get; }
    }
}