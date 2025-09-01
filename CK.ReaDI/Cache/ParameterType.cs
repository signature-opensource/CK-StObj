using CK.Engine.TypeCollector;

namespace CK.Core;

public sealed class ParameterType
{
    readonly ICachedType _type;
    internal CachedParameter _firstParameter;
    internal int _moreParameterCount;

    // Inrinsic type.
    internal ParameterType( ICachedType type )
    {
        _type = type;
    }

    ParameterType( ICachedType type, CachedParameter firstParameter )
    {
        Throw.DebugAssert( type != null && firstParameter != null );
        _type = type;
        _firstParameter = firstParameter;
    }

    public ICachedType Type => _type;

    public CachedParameter? FirstParameter => _firstParameter;

    internal static ParameterType? Create( IActivityMonitor monitor,
                                           GlobalTypeCache.WellKnownTypes wellknownTypes,
                                           ICachedType parameterType,
                                           CachedParameter p )
    {
        var t = parameterType.Type;
        if( parameterType.EngineUnhandledType != EngineUnhandledType.None
            || parameterType == wellknownTypes.Object
            || !parameterType.IsClassOrInterface
            || t.IsByRef
            || t.IsByRefLike
            || t.IsArray
            || t.IsVariableBoundArray )
        {
            monitor.Error( $"""
                    Invalid [ReaDI] method parameter '{p.ParameterType.Name} {p.Name}' in '{p.Method.ToStringWithDeclaringType()}'.
                    Type '{parameterType}' must be an interface or a regular classes (and not object).
                    """ );
            return null;
        }

        return new ParameterType( parameterType, p );
    }

    public override string ToString()
    {
        if( _firstParameter != null )
        {
            var s = $"'{_firstParameter.Name}' in '{_firstParameter.Method}'";
            if( _moreParameterCount > 0 )
            {
                s += $" (and {_moreParameterCount} other methods)";
            }
            return s;
        }
        return $"intrinsic '{_type.Name}'";
    }
}
