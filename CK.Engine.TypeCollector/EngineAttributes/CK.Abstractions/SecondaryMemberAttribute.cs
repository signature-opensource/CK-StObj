using System;

namespace CK.Core;

/// <summary>
/// Non generic base class for secondary member attributes: <see cref="SecondaryMemberAttribute{T}"/> must be used.
/// </summary>
[AttributeUsage( PrimaryMemberAttribute.Targets, AllowMultiple = true, Inherited = false )]
public abstract class SecondaryMemberAttribute : Attribute
{
    private protected SecondaryMemberAttribute( string actualAttributeTypeAssemblyQualifiedName )
    {
        ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
    }

    /// <summary>
    /// Gets the Assembly Qualified Name of the object that will replace this attribute during setup.
    /// </summary>
    public string ActualAttributeTypeAssemblyQualifiedName { get; }

    /// <summary>
    /// Gets the type of the <see cref="PrimaryMemberAttribute"/> that this attribute extends.
    /// </summary>
    public abstract Type PrimaryType { get; }
}

