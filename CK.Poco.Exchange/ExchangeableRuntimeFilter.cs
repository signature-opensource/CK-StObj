using System;
using System.Collections.Immutable;
using System.Reflection;

namespace CK.Core;

/// <summary>
/// Opaque class that provides type filtering to the serialization layer.
/// <para>
/// This sealed and immutable class is public to avoid call indirections but
/// it is not intended to be used directly. Available runtime filters are
/// exposed by the <see cref="PocoExchangeService.RuntimeFilters"/>.
/// </para>
/// </summary>
public sealed class ExchangeableRuntimeFilter
{
    readonly string _name;
    readonly ImmutableArray<int> _flags;
    readonly PocoDirectory _pocoDirectory;

    /// <summary>
    /// Not to be used directly: this is initialized by generated code.
    /// </summary>
    /// <param name="pocoDirectory">The Poco directory.</param>
    /// <param name="name">The filter <see cref="Name"/>.</param>
    /// <param name="flags">The opaque flags used to filter types.</param>
    public ExchangeableRuntimeFilter( PocoDirectory pocoDirectory, string name, int[] flags )
    {
        _name = name;
        _flags = ImmutableArray.Create( flags );
        _pocoDirectory = pocoDirectory;
    }

    /// <summary>
    /// Gets this runtime type filter name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the opaque filter flags.
    /// </summary>
    public ImmutableArray<int> Flags => _flags;

    /// <summary>
    /// Gets whether a type can be exported or imported by this filter.
    /// </summary>
    /// <param name="t">The type to test.</param>
    /// <returns>True if this filter allows the type to be exported and imported. False otherwise.</returns>
    public bool Contains( Type t )
    {
        int idx = _pocoDirectory.GetNonNullableFinalTypeIndex( t );
        return idx >= 0 ? Contains( idx ) : false;
    }

    /// <summary>
    /// Gets whether a type can be exported or imported.
    /// <para>
    /// This is not intended to be used directly: this is used by the serialization and deserialization generated code.
    /// </para>
    /// </summary>
    /// <param name="index">The type index.</param>
    /// <returns>True if the type can be exported or imported, false otherwise.</returns>
    public bool Contains( int index ) => (_flags[index >> 5] & (1 << index)) != 0;


}
