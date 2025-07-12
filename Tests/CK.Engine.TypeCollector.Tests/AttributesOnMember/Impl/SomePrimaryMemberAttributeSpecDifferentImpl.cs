namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Independent implementation for attribute <see cref="SomePrimaryMemberSpecAttribute"/>.
/// </summary>
public class SomePrimaryMemberAttributeSpecDifferentImpl : PrimaryMemberAttributeImpl<SomePrimaryMemberSpecAttribute>,
                                                           ISomePrimaryMemberSpecBehavior
{
    public string DoSomethingWithTheSpecAttribute()
    {
        return $"Totally independent [Spec] implementation can do anything with {Attribute.SomethingMore}.";
    }
}
