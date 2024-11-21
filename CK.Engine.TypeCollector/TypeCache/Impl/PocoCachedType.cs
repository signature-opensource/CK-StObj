using System;
using System.Collections.Immutable;

namespace CK.Engine.TypeCollector;

sealed class PocoCachedType : CachedType, IPocoCachedType
{
    internal PocoCachedType( GlobalTypeCache cache,
                             Type type,
                             int typeDepth,
                             CachedAssembly assembly,
                             ImmutableArray<ICachedType> interfaces,
                             ICachedType? baseType )
        : base( cache, type, typeDepth, assembly, interfaces, baseType )
    {
    }
}
