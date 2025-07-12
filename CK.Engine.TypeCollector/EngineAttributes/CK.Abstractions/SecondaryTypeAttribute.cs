using System;

namespace CK.Core;

/// <summary>
/// Non generic base class for secondary type attributes: <see cref="SecondaryTypeAttribute{T}"/> must be used.
/// </summary>
[AttributeUsage( PrimaryTypeAttribute.Targets, AllowMultiple = true, Inherited = false )]
public abstract class SecondaryTypeAttribute : Attribute
{
    private protected SecondaryTypeAttribute( string actualAttributeTypeAssemblyQualifiedName )
    {
        ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
    }

    /// <summary>
    /// Gets the Assembly Qualified Name of the CK.TypeCollector.SecondaryTypeAttributeImpl that will replace
    /// this attribute during setup.
    /// </summary>
    public string ActualAttributeTypeAssemblyQualifiedName { get; }

    /// <summary>
    /// Gets the type of the <see cref="PrimaryTypeAttribute"/> that this attribute extends.
    /// </summary>
    public abstract Type PrimaryType { get; }
}

