namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Specialized attribute that can be bound to:
/// <list type="bullet">
///     <item>
///     The same implementation as its base (SomePrimaryMemberAttributeImpl).
///     It is up to this implementation to handle this specialized attribute (this can
///     be useful is some -simple- scenario).
///     </item>
///     <item>
///     A specialized implementation of the base SomePrimaryMemberAttributeImpl.
///     The <see cref="SomePrimaryMemberSpecAttributeImpl"/> inherits SomePrimaryMemberAttributeImpl.
///     This is useful to exend the behavior of a Attribute/Impl in other packages
///     (providing that the attribute is not sealed and the base Impl is also not sealed
///     and has some useful extension points).
///     </item>
///     <item>
///     A completely different implementation (here <see cref="SomePrimaryMemberAttributeSpecDifferentImpl"/>).
///     </item>
/// </list>
/// </summary>
public sealed class SomePrimaryMemberSpecAttribute : SomePrimaryMemberAttribute
{
    /// <summary>
    /// Option to the base "Spec" attribute.
    /// </summary>
    public int SomethingMore { get; }

    /// <summary>
    /// Demo of different possible bindings.
    /// This obviously is a demo (and a test), in real life this should hardly appear!
    /// </summary>
    public enum ImplBinding
    {
        SameAsBase,
        SpecializedImpl,
        DifferentImpl
    }

    public SomePrimaryMemberSpecAttribute( string name, ImplBinding binding, int somethingMore )
        : base( binding switch
                {
                    ImplBinding.SameAsBase => "CK.Engine.TypeCollector.Tests.SomePrimaryMemberAttributeImpl, CK.Engine.TypeCollector.Tests",
                    ImplBinding.SpecializedImpl => "CK.Engine.TypeCollector.Tests.SomePrimaryMemberSpecAttributeImpl, CK.Engine.TypeCollector.Tests",
                    _ => "CK.Engine.TypeCollector.Tests.SomePrimaryMemberAttributeSpecDifferentImpl, CK.Engine.TypeCollector.Tests"
                },
                name + "[Spec]" )
    {
        SomethingMore = somethingMore;
    }
}
