using CK.Engine.TypeCollector;

namespace CK.Core;

public sealed class ProducedType
{
    readonly ICachedType _type;
    internal CallableType _firstCreator;
    internal int _moreCreatorCount;

    ProducedType( ICachedType type, CallableType firstCreator )
    {
        Throw.DebugAssert( type != null && firstCreator != null );
        _type = type;
        _firstCreator = firstCreator;
    }

    public ICachedType Type => _type;

    public CallableType? FirstCreator => _firstCreator;

    internal static ProducedType? Create( IActivityMonitor monitor,
                                           GlobalTypeCache.WellKnownTypes wellknownTypes,
                                           ICachedType parameterType,
                                           CachedParameter p )
    {
        return new ProducedType( parameterType, p );
    }

    public override string ToString()
    {
        return $"return of '{_firstCreator}'";
    }
}
