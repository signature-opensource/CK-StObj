using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Engine.TypeCollector;

sealed class ImmutableConfiguredTypeSet : IConfiguredTypeSet
{
    public static readonly ImmutableConfiguredTypeSet Empty = new ImmutableConfiguredTypeSet();

    readonly IReadOnlyCollection<TypeConfiguration> _configuredTypes;

    readonly IReadOnlySet<Type> _allTypes;

    // internal: don't use a ImmutableHashSet.
    internal ImmutableConfiguredTypeSet( HashSet<Type> types )
    {
        _allTypes = types;
        _configuredTypes = ImmutableArray<TypeConfiguration>.Empty;
    }

    ImmutableConfiguredTypeSet()
    {
        _allTypes = ImmutableHashSet<Type>.Empty;
        _configuredTypes = ImmutableArray<TypeConfiguration>.Empty;
    }

    public IReadOnlySet<Type> AllTypes => _allTypes;

    public IReadOnlyCollection<TypeConfiguration> ConfiguredTypes => _configuredTypes;
}
