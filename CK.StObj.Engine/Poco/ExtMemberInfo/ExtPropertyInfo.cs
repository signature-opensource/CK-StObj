using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup;

sealed class ExtPropertyInfo : ExtMemberInfoBase, IExtPropertyInfo
{
    public ExtPropertyInfo( ExtMemberInfoFactory factory, PropertyInfo p )
        : base( factory, p )
    {
    }

    internal ExtPropertyInfo( ExtMemberInfoFactory factory,
                              PropertyInfo fake,
                              IExtNullabilityInfo homogeneousInfo,
                              object[]? customAttributes,
                              CustomAttributeData[]? customAttributesData )
        : base( factory, fake, homogeneousInfo, customAttributes, customAttributesData )
    {
    }

    public PropertyInfo PropertyInfo => Unsafe.As<PropertyInfo>( _o );
}
