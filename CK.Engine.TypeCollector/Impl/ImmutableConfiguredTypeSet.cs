using CK.Setup;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Engine.TypeCollector;

sealed class ImmutableConfiguredTypeSet : IConfiguredTypeSet
{
    public static readonly ImmutableConfiguredTypeSet Empty = new ImmutableConfiguredTypeSet();

    readonly IReadOnlyCollection<TypeConfiguration> _configuredTypes;

    readonly IReadOnlySet<ICachedType> _allTypes;

    // internal: don't use a ImmutableHashSet.
    internal ImmutableConfiguredTypeSet( HashSet<ICachedType> types )
    {
        _allTypes = types;
        _configuredTypes = ImmutableArray<TypeConfiguration>.Empty;
    }

    // Empty.
    ImmutableConfiguredTypeSet()
    {
        _allTypes = ImmutableHashSet<ICachedType>.Empty;
        _configuredTypes = ImmutableArray<TypeConfiguration>.Empty;
    }

    public IReadOnlySet<ICachedType> AllTypes => _allTypes;

    public IReadOnlyCollection<TypeConfiguration> ConfiguredTypes => _configuredTypes;
}
