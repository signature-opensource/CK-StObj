using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CK.Engine.TypeCollector;

abstract partial class CachedItem : ICachedItem
{
    private protected readonly MemberInfo _member;
    ImmutableArray<CustomAttributeData> _customAttributes;
    ImmutableArray<object> _attributes;
    ImmutableArray<object> _finalAttributes;
    bool _finalAttributesInitialized;

    internal CachedItem( MemberInfo member )
    {
        _member = member;
    }

    public string Name => _member.Name;

    public ImmutableArray<CustomAttributeData> AttributesData
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


    public ImmutableArray<object> RawAttributes
    {
        get
        {
            if( _attributes.IsDefault )
            {
                _attributes = ImmutableCollectionsMarshal.AsImmutableArray( _member.GetCustomAttributes( inherit: false ) );
            }
            return _attributes;
        }
    }

    public abstract StringBuilder Write( StringBuilder b );
}
