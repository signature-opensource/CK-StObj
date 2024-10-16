using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract class CachedItem : ICachedItem
{
    private protected readonly MemberInfo _member;
    ImmutableArray<CustomAttributeData> _customAttributes;

    internal CachedItem( MemberInfo member )
    {
        _member = member;
    }

    public string Name => _member.Name;

    public ImmutableArray<CustomAttributeData> CustomAttributes
    {
        get
        {
            if( _customAttributes.IsDefault )
            {
                // Cannot use ImmutableCollectionsMarshal.AsImmutableArray here: CustomAttributeData
                // can be retrieved by IList<CustomAttributeData> or IEnumerable<CustomAttributeData> only.
                _customAttributes = _member.CustomAttributes.ToImmutableArray();
            }
            return _customAttributes;
        }
    }

    public abstract StringBuilder Write( StringBuilder b );
}
