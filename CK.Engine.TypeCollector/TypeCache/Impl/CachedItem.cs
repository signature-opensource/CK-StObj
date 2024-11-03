using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract class CachedItem : ICachedItem
{
    private protected readonly MemberInfo _member;
    ImmutableArray<CustomAttributeData> _customAttributes;
    ImmutableArray<object> _attributes;

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

    public ImmutableArray<object> Attributes
    {
        get
        {
            if( _attributes.IsDefault )
            {
                _attributes = ImmutableCollectionsMarshal.AsImmutableArray( _member.GetCustomAttributes(false) );
            }
            return _attributes;
        }
    }

    public abstract StringBuilder Write( StringBuilder b );
}
