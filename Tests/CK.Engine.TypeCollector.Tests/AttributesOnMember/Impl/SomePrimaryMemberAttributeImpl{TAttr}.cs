using System.Runtime.CompilerServices;

namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Strongly typed Attribute adapter.
/// </summary>
/// <typeparam name="TAttr">The attribute type that specializes SomePrimaryMemberAttribute.</typeparam>
public class SomePrimaryMemberAttributeImpl<TAttr> : SomePrimaryMemberAttributeImpl
    where TAttr : SomePrimaryMemberAttribute
{
    /// <summary>
    /// Gets the strongly typed attribute.
    /// </summary>
    public new TAttr Attribute => Unsafe.As<TAttr>( base.Attribute );
}
