using System;
using System.Reflection;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Captures <see cref="ICachedType.GenericParameters"/> information.
/// </summary>
public sealed class CachedGenericParameter
{
    readonly Type _type;
    readonly GenericCachedTypeDefinition _declaringType;

    internal CachedGenericParameter( GenericCachedTypeDefinition declaringType, Type type )
    {
        _type = type;
        _declaringType = declaringType;
    }

    /// <summary>
    /// Gets the type argument. Only its <see cref="MemberInfo.Name"/> is usable.
    /// </summary>
    public Type Type => _type;

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name => _type.Name;

    /// <summary>
    /// Gets the parameter attributes.
    /// </summary>
    public GenericParameterAttributes Attributes => _type.GenericParameterAttributes;

    /// <summary>
    /// Gets the generic type definition that declares this generic parameter.
    /// </summary>
    public ICachedType DeclaringType => _declaringType;

    public override string ToString() => Name;
}
