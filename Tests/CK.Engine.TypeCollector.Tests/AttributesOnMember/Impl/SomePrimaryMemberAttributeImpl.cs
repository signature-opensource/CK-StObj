using CK.Core;
using Shouldly;
using System.Linq;

namespace CK.Engine.TypeCollector.Tests;

/// <summary>
/// Specializable. The <see cref="SomePrimaryMemberAttributeImpl{TAttr}"/> provides a strongly typed Attribute.
/// </summary>
public class SomePrimaryMemberAttributeImpl : PrimaryMemberAttributeImpl<SomePrimaryMemberAttribute>,
                                              IAttributeHasNameProperty,
                                              ISomePrimaryMemberSpecBehavior
{
    public string TheAttributeName => Attribute.Name;

    /// <summary>
    /// This is virtual: a specialization can alter this behavior, <see cref="SomePrimaryMemberSpecAttributeImpl"/>
    /// does that.
    /// </summary>
    /// <returns></returns>
    public virtual string DoSomethingWithTheSpecAttribute()
    {
        if( Attribute is SomePrimaryMemberSpecAttribute spec )
        {
            return $"My Attribute is a Spec and it has {spec.SomethingMore}.";
        }
        return $"My Attribute is a NOT a Spec. I give up.";
    }
}
