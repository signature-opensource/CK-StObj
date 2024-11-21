using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

sealed class ExtTypeInfo : IExtTypeInfo
{
    readonly ExtMemberInfoFactory _factory;
    readonly Type _type;
    IExtTypeInfo? _baseType;
    string? _typeName;
    IReadOnlyList<object>? _customAttributes;
    IReadOnlyList<CustomAttributeData>? _customAttributesData;
    IExtNullabilityInfo? _nullabilityInfo;

    public ExtTypeInfo( ExtMemberInfoFactory factory, Type type )
    {
        _factory = factory;
        if( type.IsByRef ) type = type.GetElementType()!;
        _type = type;
    }

    public Type DeclaringType => _type;

    public Type Type => _type;

    public object UnderlyingObject => _type;

    public string Name => _type.Name;

    public string TypeCSharpName => _typeName ??= _type.ToCSharpName();

    public IReadOnlyList<CustomAttributeData> CustomAttributesData => _customAttributesData ??= (IReadOnlyList<CustomAttributeData>)_type.GetCustomAttributesData();

    public IReadOnlyList<object> CustomAttributes => _customAttributes ??= _type.GetCustomAttributes( false );

    public IExtNullabilityInfo? HomogeneousNullabilityInfo => ReadNullabilityInfo;

    public IExtNullabilityInfo ReadNullabilityInfo => _nullabilityInfo ??= _factory.CreateNullabilityInfo( _type );

    public IExtNullabilityInfo WriteNullabilityInfo => ReadNullabilityInfo;

    public IExtTypeInfo? BaseType => _baseType ??= _type.BaseType != null ? _factory.CreateNullOblivious( _type.BaseType ) : null;

    public override string ToString() => TypeCSharpName;
}
