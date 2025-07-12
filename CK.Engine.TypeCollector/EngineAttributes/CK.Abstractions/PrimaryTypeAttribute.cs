using System;

namespace CK.Core;

/// <summary>
/// Abstract base class for primary type attributes.
/// <para>
/// This family of attributes (this one, <see cref="SecondaryTypeAttribute{T}"/>, <see cref="PrimaryMemberAttribute"/>
/// and <see cref="SecondaryMemberAttribute{T}"/>) bind the user code to the Engine side through a simple assembly
/// qualified name that identifies the actual attribute. The user code side attribute can declare any
/// properties that configures the behavior of its Engine peer.
/// </para>
/// <para>
/// A <see cref="PrimaryTypeAttribute"/> can be "augmented" by any number of <see cref="SecondaryTypeAttribute{T}"/>.
/// Usually, a type is decorated by only one PrimaryTypeAttribute but nothing prevents different PrimaryTypeAttribute
/// to decorate the same type: it is up to the Engine side to define the behavior.
/// </para>
/// <para>
/// When specializing, the:
/// <c>[AttributeUsage( <see cref="Targets"/>, AllowMultiple = false, Inherited = false )]</c>
/// should be redefined.
/// Specializations that don't redefine it are mutually exclusive.
/// <para>
/// Note that AllowMultiple is false by default, but Inherited is true by default and must be false).
/// The attribute targets should be restricted if it makes sense for the attribute.  
/// </para>
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface|AttributeTargets.Enum )]
public abstract class PrimaryTypeAttribute : Attribute
{
    /// <summary>
    /// Defines the default member targets.
    /// </summary>
    public const AttributeTargets Targets = AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Enum;

    /// <summary>
    /// Initializes a new <see cref="PrimaryTypeAttribute"/> that delegates its behaviors to
    /// an Engine implementation.
    /// </summary>
    /// <param name="actualAttributeTypeAssemblyQualifiedName">
    /// Assembly Qualified Name of the associated engine implementation that must be a specialized
    /// CK.Engine.TypeCollector.PrimaryTypeAttributeImpl class.
    /// <para>
    /// Example: "CK.TypeScript.Engine.TypeScriptPackageImpl, CK.TypeScript.Engine".
    /// </para>
    /// </param>
    protected PrimaryTypeAttribute( string actualAttributeTypeAssemblyQualifiedName )
    {
        ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
    }


    /// <summary>
    /// Gets the Assembly Qualified Name of the object that will replace this attribute during setup.
    /// </summary>
    public string ActualAttributeTypeAssemblyQualifiedName { get; }
}
