using System;

namespace CK.Core;

/// <summary>
/// Abstract base class for secondary type attributes.
/// See <see cref="PrimaryTypeAttribute"/>.
/// <para>
/// When specializing, the [AttributeUsage( PrimaryTypeAttribute.Targets, AllowMultiple = true, Inherited = false )]
/// doesn't need to be redefined (but it could be if the attribute must not be applicable to all the
/// default <see cref="PrimaryTypeAttribute.Targets"/>).
/// </para>
/// </summary>
/// <typeparam name="T">The <see cref="PrimaryTypeAttribute"/> that this attribute extends.</typeparam>
public abstract class SecondaryTypeAttribute<T> : SecondaryTypeAttribute where T : PrimaryTypeAttribute
{
    /// <summary>
    /// Initializes a new <see cref="SecondaryTypeAttribute{T}"/> that delegates its behaviors to
    /// an Engine implementation.
    /// </summary>
    /// <param name="actualAttributeTypeAssemblyQualifiedName">
    /// Assembly Qualified Name of the associated engine implementation that must be a specialized
    /// CK.Engine.TypeCollector.SecondaryTypeAttributeImpl class.
    /// <para>
    /// Example: "CK.TypeScript.Engine.TypeScriptImportLibraryAttributeImpl, CK.TypeScript.Engine".
    /// </para>
    /// </param>
    protected SecondaryTypeAttribute( string actualAttributeTypeAssemblyQualifiedName )
        : base( actualAttributeTypeAssemblyQualifiedName )
    {
    }

    /// <inheritdoc />
    public override sealed Type PrimaryType => typeof(T);
}
