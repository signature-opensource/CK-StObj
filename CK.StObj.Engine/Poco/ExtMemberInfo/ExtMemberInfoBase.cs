using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace CK.Setup;


class ExtMemberInfoBase : IExtMemberInfo
{
    readonly ExtMemberInfoFactory _factory;
    internal readonly ICustomAttributeProvider _o;
    readonly string _name;
    readonly Type _type;
    readonly Type _declaringType;

    string? _typeName;
    IExtNullabilityInfo? _rNullabilityInfo;
    IExtNullabilityInfo? _wNullabilityInfo;
    IReadOnlyList<object>? _customAttributes;
    IReadOnlyList<CustomAttributeData>? _customAttributesData;

    protected ExtMemberInfoBase( ExtMemberInfoFactory factory,
                                 PropertyInfo fake,
                                 IExtNullabilityInfo info,
                                 object[]? customAttributes,
                                 CustomAttributeData[]? customAttributesData )
    {
        _factory = factory;
        _o = fake;
        _name = fake.Name;
        _type = info.Type;
        _declaringType = fake.DeclaringType!;
        Throw.DebugAssert( info.IsHomogeneous );
        _rNullabilityInfo = _wNullabilityInfo = info;
        _customAttributes = customAttributes ?? Array.Empty<object>();
        _customAttributesData = customAttributesData ?? Array.Empty<CustomAttributeData>();
    }

    internal ExtMemberInfoBase( ExtMemberInfoFactory factory, ICustomAttributeProvider o )
    {
        _factory = factory;
        _o = o;
        switch( o )
        {
            case MemberInfo m:
                // Check once for all that we are not handling a globally declared type
                // (at the Module level). See https://stackoverflow.com/a/35266094/190380
                _declaringType = CheckDeclaringType( m );
                _name = m.Name;
                _type = m switch
                {
                    PropertyInfo p => p.PropertyType,
                    FieldInfo f => f.FieldType,
                    // Only non C# assemblies can have an handler type that
                    // is not a delegate. We deliberately ignore this case.
                    EventInfo e => e.EventHandlerType!,
                    _ => Throw.NotSupportedException<Type>()
                };
                break;
            case ParameterInfo p:
                _name = p.Name ?? String.Empty;
                _type = p.ParameterType;
                _declaringType = CheckDeclaringType( p.Member );
                break;
            default: Throw.NotSupportedException(); break;
        };

        static Type CheckDeclaringType( MemberInfo m )
        {
            if( m.DeclaringType == null )
            {
                Throw.InvalidOperationException( $"Globally defined members (Module level) are not allowed. Member: {m}." );
            }
            return m.DeclaringType;
        }
    }

    public string Name => _name;

    public object UnderlyingObject => _o;

    public Type Type => _type;

    public Type DeclaringType => _declaringType;

    public IReadOnlyList<CustomAttributeData> CustomAttributesData
    {
        get
        {
            return _customAttributesData ??= _o switch
            {
                MemberInfo m => (IReadOnlyList<CustomAttributeData>)m.GetCustomAttributesData(),
                ParameterInfo p => (IReadOnlyList<CustomAttributeData>)p.GetCustomAttributesData(),
                _ => Throw.NotSupportedException<CustomAttributeData[]>(),
            };
        }
    }

    public IReadOnlyList<object> CustomAttributes => _customAttributes ??= _o.GetCustomAttributes( false );

    public string TypeCSharpName
    {
        get
        {
            if( _typeName == null )
            {
                var t = _type.IsByRef ? _type.GetElementType() : _type;
                _typeName = t.ToCSharpName();
            }
            return _typeName;
        }
    }

    public IExtNullabilityInfo? HomogeneousNullabilityInfo => ReadNullabilityInfo.IsHomogeneous ? _rNullabilityInfo : null;

    public IExtNullabilityInfo ReadNullabilityInfo
    {
        get
        {
            if( _rNullabilityInfo == null )
            {
                _rNullabilityInfo = _o switch
                {
                    ParameterInfo p => _factory.CreateNullabilityInfo( p, true ),
                    PropertyInfo p => _factory.CreateNullabilityInfo( p, true ),
                    FieldInfo p => _factory.CreateNullabilityInfo( p, true ),
                    EventInfo p => _factory.CreateNullabilityInfo( p ),
                    _ => Throw.NotSupportedException<IExtNullabilityInfo>()
                };
                if( _rNullabilityInfo.IsHomogeneous ) _wNullabilityInfo ??= _rNullabilityInfo;
            }
            return _rNullabilityInfo;
        }
    }

    public IExtNullabilityInfo WriteNullabilityInfo
    {
        get
        {
            if( _wNullabilityInfo == null )
            {
                _wNullabilityInfo = _o switch
                {
                    ParameterInfo p => _factory.CreateNullabilityInfo( p, false ),
                    PropertyInfo p => _factory.CreateNullabilityInfo( p, false ),
                    FieldInfo p => _factory.CreateNullabilityInfo( p, false ),
                    EventInfo => ReadNullabilityInfo,
                    _ => Throw.NotSupportedException<IExtNullabilityInfo>()
                };
                if( _wNullabilityInfo.IsHomogeneous ) _rNullabilityInfo ??= _wNullabilityInfo;
            }
            return _wNullabilityInfo;
        }
    }

    public override string ToString() => _o switch
    {
        PropertyInfo p => $"Property '{p.DeclaringType.ToCSharpName()}.{p.Name}'",
        FieldInfo f => $"Field '{f.DeclaringType.ToCSharpName()}.{f.Name}'",
        EventInfo e => $"Event '{e.DeclaringType.ToCSharpName()}.{e.Name}'",
        ParameterInfo p => $"Parameter '{p.Name}' of method '{p.Member.DeclaringType.ToCSharpName()}.{p.Member.Name}'",
        _ => Throw.NotSupportedException<string>()
    };
}
