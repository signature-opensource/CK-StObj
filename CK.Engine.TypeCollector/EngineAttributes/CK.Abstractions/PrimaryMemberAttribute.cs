using System;

namespace CK.Core;

/// <summary>
/// Abstract base class for primary member attributes.
/// Same as the <see cref="PrimaryTypeAttribute"/>/<see cref="SecondaryTypeAttribute{T}"/> pattern but
/// for <see cref="Targets"/>.
/// <para>
/// When specializing, the
/// <c>[AttributeUsage( <see cref="Targets"/>, AllowMultiple = false, Inherited = false )]</c>
/// should be redefined. Specializations that don't redefine it are mutually exclusive.
/// <para>
/// Note that AllowMultiple is false by default, but Inherited is true by default and must be false).
/// The attribute targets should be restricted if it makes sense for the attribute.  
/// </para>
/// </para>
/// </summary>
[AttributeUsage( Targets, AllowMultiple = false, Inherited = false )]
public abstract class PrimaryMemberAttribute : Attribute
{
    /// <summary>
    /// Defines the default member targets.
    /// </summary>
    public const AttributeTargets Targets = AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Constructor | AttributeTargets.Method;

    /// <summary>
    /// Initializes a new <see cref="PrimaryMemberAttribute"/> that delegates its behaviors to
    /// an Engine implementation.
    /// </summary>
    /// <param name="actualAttributeTypeAssemblyQualifiedName">
    /// Assembly Qualified Name of the associated engine implementation that must be a specialized
    /// CK.Engine.TypeCollector.PrimaryMemberAttributeImpl class.
    /// <para>
    /// Example: "CK.DB.Engine.SqlProcedureImpl, CK.DB.Engine".
    /// </para>
    /// </param>
    protected PrimaryMemberAttribute( string actualAttributeTypeAssemblyQualifiedName )
    {
        ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
    }

    /// <summary>
    /// Gets the Assembly Qualified Name of the object that will replace this attribute during setup.
    /// </summary>
    public string ActualAttributeTypeAssemblyQualifiedName { get; }
}
