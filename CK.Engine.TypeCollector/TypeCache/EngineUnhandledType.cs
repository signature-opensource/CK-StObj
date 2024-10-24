using CK.Core;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Defines special type case that engines should ignore.
/// </summary>
public enum EngineUnhandledType : byte
{
    /// <summary>
    /// Regular visible type.
    /// </summary>
    None,

    /// <summary>
    /// The Type.FullName is null. This happens if the current instance represents a generic type parameter,
    /// an array type, pointer type, or byref type based on a type parameter, or a generic type
    /// that is not a generic type definition but contains unresolved type parameters.
    /// FullName is also null for (at least) classes nested into nested generic classes.
    /// </summary>
    NullFullName,

    /// <summary>
    /// The type is implemented in a dynamic assembly.
    /// </summary>
    FromDynamicAssembly,

    /// <summary>
    /// The type is not visible outside of its assembly.
    /// </summary>
    NotVisible,

    /// <summary>
    /// The type is not a class, an enum, a value type or an interface.
    /// </summary>
    NotClassEnumValueTypeOrEnum
}

static class Unhandled
{
    public static string? GetUnhandledMessage( this EngineUnhandledType type ) =>
            type switch
            {
                EngineUnhandledType.None => null,
                EngineUnhandledType.NullFullName => "has a null FullName",
                EngineUnhandledType.FromDynamicAssembly => "is defined by a dynamic assembly",
                EngineUnhandledType.NotVisible => "must be public (visible outside of its asssembly)",
                EngineUnhandledType.NotClassEnumValueTypeOrEnum => "must be an enum, a value type, a class or an interface",
                _ => Throw.NotSupportedException<string>( type.ToString() )
            };
}
