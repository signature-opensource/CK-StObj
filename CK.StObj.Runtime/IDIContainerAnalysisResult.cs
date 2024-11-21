using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup;

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
    /// Lists all the ambient service types where <see cref="IAutoService"/> inheritance chains
    /// are expanded.
    /// <para>
    /// Order matters: consecutive entries with
    /// the same <see cref="DIContainerHub.AmbientServiceMapping.MappingIndex"/> belong to the same auto service inheritance
    /// chain.
    /// </para>
    /// <para>
    /// See <see cref="DIContainerHub.AmbientServiceMapping"/>. 
    /// </para>
    /// </summary>
    IReadOnlyList<DIContainerHub.AmbientServiceMapping> AmbientServiceMappings { get; }

    /// <summary>
    /// Captures the ambient service default provider implementation.
    /// When <see cref="IsValid"/> is false, this default must be ignored: the ambient service
    /// has no need for a default value provider.
    /// This is the case of the <see cref="AmbientServiceHub"/>.
    /// </summary>
    public readonly struct AmbientServiceDefault
    {
        /// <summary>
        /// Gets whether this default is valid.
        /// </summary>
        public bool IsValid => ProviderType != null;

        /// <summary>
        /// Gets the <see cref="IAmbientServiceDefaultProvider{T}"/> type that the
        /// <see cref="Provider"/> implements.
        /// <para>
        /// <see cref="IsValid"/> MUST be true for this to used.
        /// </para>
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// Gets the service implementation.
        /// <para>
        /// <see cref="IsValid"/> MUST be true for this to used.
        /// </para>
        /// </summary>
        public IStObjFinalClass Provider { get; }

        /// <summary>
        /// Initializes a new default.
        /// </summary>
        /// <param name="providerType">The <see cref="IAmbientServiceDefaultProvider{T}"/> type.</param>
        /// <param name="provider">The service implementation.</param>
        public AmbientServiceDefault( Type providerType, IStObjFinalClass provider )
        {
            ProviderType = providerType;
            Provider = provider;
        }
    }

    /// <summary>
    /// Gets the <see cref="IAmbientServiceDefaultProvider{T}"/> to use for each mapped ambient
    /// service.
    /// </summary>
    IReadOnlyList<AmbientServiceDefault> DefaultAmbientServiceValueProviders { get; }
}
