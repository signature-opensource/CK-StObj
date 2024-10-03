using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Core;

/// <summary>
/// Empty map is valid but has nothing to configure.
/// </summary>
public class EmptyCKomposableMap : IStObjMap, IStObjObjectMap, IStObjServiceMap
{
    /// <inheritdoc />
    public IStObjObjectMap StObjs => this;

    /// <summary>
    /// Gets <see cref="SHA1Value.Zero"/>.
    /// </summary>
    public SHA1Value GeneratedSignature => SHA1Value.Zero;

    public IStObjServiceMap Services => this;

    /// <summary>
    /// Gets an empty list of names.
    /// </summary>
    public IReadOnlyList<string> Names => ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets an empty set of features.
    /// </summary>
    public IReadOnlyCollection<VFeature> Features => ImmutableArray<VFeature>.Empty;

    /// <summary>
    /// Gets an empty mapping dictionary.
    /// </summary>
    public IReadOnlyDictionary<Type, IStObjMultipleInterface> MultipleMappings => ImmutableDictionary<Type, IStObjMultipleInterface>.Empty;

    /// <summary>
    /// Warns and returs true.
    /// </summary>
    /// <param name="serviceRegister">The service register helper.</param>
    public bool ConfigureServices( in StObjContextRoot.ServiceRegister serviceRegister )
    {
        serviceRegister.Monitor.Warn( $"Empty CKomposable map has no services to configure." );
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns null.
    /// </remarks>
    public IStObjFinalClass? ToLeaf( Type t ) => null;

    IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => ImmutableDictionary<Type, IStObjFinalImplementation>.Empty;

    IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => ImmutableArray<IStObjFinalImplementation>.Empty;

    IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.Mappings => ImmutableDictionary<Type, IStObjServiceClassDescriptor>.Empty;

    IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.MappingList => ImmutableArray<IStObjServiceClassDescriptor>.Empty;

    IReadOnlyList<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => ImmutableArray<IStObjFinalImplementation>.Empty;

    IEnumerable<StObjMapping> IStObjObjectMap.StObjs => ImmutableArray<StObjMapping>.Empty;

    object? IStObjObjectMap.Obtain( Type t ) => null;

    IStObjFinalImplementation? IStObjObjectMap.ToLeaf( Type t ) => null;
}
