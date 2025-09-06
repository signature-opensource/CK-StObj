using System.Collections.Generic;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Extends Type Cache related types.
/// </summary>
public static class CachedTypeExtensions
{
    /// <summary>
    /// Computes a set of (AlsoType, SourceType) from this type set. This returns null if this type set is
    /// already complete. The returned dictionary's keys should be added to this set. Some key (discovered
    /// AlsoType) may already exist in this type set.
    /// The SourceType value is the first (random) type that has triggered
    /// the registration of the AlsoType.
    /// </summary>
    /// <param name="typeSet">The initial type set.</param>
    /// <returns>A set of (AlsoType, SourceType) or null if this set is complete.</returns>
    public static Dictionary<ICachedType, ICachedType>? GetRegisterAlsoTypesClosure( this IReadOnlySet<ICachedType> typeSet )
    {
        Dictionary<ICachedType, ICachedType>? result = null;
        foreach( var type in typeSet )
        {
            foreach( var a in type.AlsoRegisterTypes )
            {
                // If the type set already contains the also type, it has been or will be handled by the enumeration.
                // This avoids creating a useless result on an already complete set and kindly
                // handle stupid auto references.
                if( !typeSet.Contains( a ) )
                {
                    result ??= new Dictionary<ICachedType, ICachedType>();
                    if( result.TryAdd( a, type ) )
                    {
                        // We cannot exploit the initial typeSet here: the result must contain
                        // the already processed source type as keys otherwise we'll miss also types.
                        AppendClosure( a, result );
                    }
                }
            }
        }
        return result;

        static void AppendClosure( ICachedType type, Dictionary<ICachedType, ICachedType> result )
        {
            foreach( var a in type.AlsoRegisterTypes )
            {
                if( result.TryAdd( a, type ) )
                {
                    AppendClosure( a, result );
                }
            }
        }

    }
}
