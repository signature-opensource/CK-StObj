
using System;

namespace CK.Core;

/// <summary>
/// Abstract base class for secondary member attributes.
/// See <see cref="PrimaryMemberAttribute"/>.
/// <para>
/// When specializing, the [AttributeUsage( PrimaryTypeAttribute.Targets, AllowMultiple = true, Inherited = false )]
/// doesn't need to be redefined (but it could be if the attribute must not be applicable to all the
/// default <see cref="PrimaryMemberAttribute.Targets"/>).
/// </para>
/// </summary>
/// <typeparam name="T">The <see cref="PrimaryTypeAttribute"/> that this attribute extends.</typeparam>
public abstract class SecondaryMemberAttribute<T> : SecondaryMemberAttribute where T : PrimaryMemberAttribute
{
    /// <summary>
    /// Initializes a new <see cref="SecondaryMemberAttribute{T}"/> that delegates its behaviors to
    /// an Engine implementation.
    /// </summary>
    /// <param name="actualAttributeTypeAssemblyQualifiedName">
    /// Assembly Qualified Name of the associated engine implementation that must be a specialized
    /// CK.Engine.TypeCollector.SecondaryMemberAttributeImpl class.
    /// <para>
    /// Example: "Namespace.TypeNameImpl, SomeAssembly".
    /// </para>
    /// </param>
    protected SecondaryMemberAttribute( string actualAttributeTypeAssemblyQualifiedName )
        : base( actualAttributeTypeAssemblyQualifiedName )
    {
    }

    /// <inheritdoc />
    public override sealed Type PrimaryType => typeof( T );
}

