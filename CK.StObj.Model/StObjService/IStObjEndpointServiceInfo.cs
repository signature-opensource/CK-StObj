using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Captures the information about endpoint services and the ubiquitous information services.
    /// <para>
    /// Because we currently handle dynamic global service configuration (we don't impose using IRealObject.ConfigureServices
    /// only), the DI configuration cannot be fully computed at setup time and we need these data to be able to do the job.
    /// If we decide to follow the "IRealObject.ConfigureServices way", this would become useless.
    /// </para>
    /// </summary>
    public interface IStObjEndpointServiceInfo
    {
        /// <summary>
        /// Gets all the endpoint service types and their kind (they are not necessarily <see cref="IAutoService"/>).
        /// </summary>
        IReadOnlyDictionary<Type, AutoServiceKind> EndpointServices { get; }

        /// <summary>
        /// Gets whether at least one ubiquitous information service type exists.
        /// </summary>
        bool HasUbiquitousInfoServices { get; }

        /// <summary>
        /// A ubiquitous mapping supports auto service unique mappings by associating
        /// to ubiquitous auto service a single entry: <see cref="EndpointUbiquitousInfo"/> uses this
        /// when overriding a value to automatically sets all the unique mappings to the same value whatever
        /// the type used as the key.
        /// <para>
        /// This is also used to map to the <see cref="IEndpointUbiquitousServiceDefault{T}"/> that must be used when a
        /// ubiquitous service resolution is not registered by a endpoint.
        /// </para>
        /// </summary>
        public readonly struct UbiquitousMapping
        {
            /// <summary>
            /// Initializes a new mapping.
            /// </summary>
            /// <param name="ubiquitousType">The ubiquitous type.</param>
            /// <param name="mappingIndex">The index.</param>
            public UbiquitousMapping( Type ubiquitousType, int mappingIndex )
            {
                UbiquitousType = ubiquitousType;
                MappingIndex = mappingIndex;
            }

            /// <summary>
            /// The ubiquitous type.
            /// </summary>
            public Type UbiquitousType { get; }

            /// <summary>
            /// The mapping index. The same index is used for all unique mappings
            /// to the same most specialized type.
            /// </summary>
            public int MappingIndex { get; }

            public void Deconstruct( out Type t, out int i )
            {
                t = UbiquitousType;
                i = MappingIndex;
            }
        }

        /// <summary>
        /// Lists all the ubiquitous service types where <see cref="IAutoService"/> inheritance chains
        /// are expanded. See <see cref="UbiquitousMapping"/>. Order matters: consecutive entries with
        /// the same <see cref="UbiquitousMapping.MappingIndex"/> belong to the same auto service inheritance
        /// chain.
        /// </summary>
        IReadOnlyList<UbiquitousMapping> UbiquitousMappings { get; }

        /// <summary>
        /// Gets the <see cref="IEndpointUbiquitousServiceDefault{T}"/> to use for each mapped ubiquitous
        /// service.
        /// </summary>
        IReadOnlyList<IStObjFinalClass> DefaultUbiquitousValueProviders { get; }

    }
}
