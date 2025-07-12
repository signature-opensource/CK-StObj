namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Specialized SomePrimaryMemberAttributeImpl with a strongly typed Attribute thanks to
/// the <see cref="SomePrimaryMemberAttributeImpl{TAttr}"/> helper.
/// <para>
/// See <see cref="SomePrimaryMemberSpecAttribute"/>.
/// </para>
/// </summary>
public class SomePrimaryMemberSpecAttributeImpl : SomePrimaryMemberAttributeImpl<SomePrimaryMemberSpecAttribute>
{
    public override string DoSomethingWithTheSpecAttribute()
    {
        return $"I'm the [Spec] implementation, I can do stuff with {Attribute.SomethingMore}.";
    }
}
