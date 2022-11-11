using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace CK.Setup
{
    sealed class ExtMemberInfo : IExtMemberInfo, IExtPropertyInfo, IExtFieldInfo, IExtEventInfo, IExtParameterInfo
    {
        readonly ExtMemberInfoFactory _factory;
        readonly ICustomAttributeProvider _o;
        readonly string _name;
        readonly Type _type;
        readonly Type _declaringType;

        string? _typeName;
        IExtNullabilityInfo? _rNullabilityInfo;
        IExtNullabilityInfo? _wNullabilityInfo;
        object[]? _customAttributes;
        CustomAttributeData[]? _customAttributesData;

        internal ExtMemberInfo( ExtMemberInfoFactory factory, ICustomAttributeProvider o )
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

        ParameterInfo IExtParameterInfo.ParameterInfo => (ParameterInfo)_o;

        public IExtParameterInfo? AsParameterInfo => _o is ParameterInfo ? this : null;

        PropertyInfo IExtPropertyInfo.PropertyInfo => (PropertyInfo)_o;

        public IExtPropertyInfo? AsPropertyInfo => _o is PropertyInfo ? this : null;

        FieldInfo IExtFieldInfo.FieldInfo => (FieldInfo)_o;

        public IExtFieldInfo? AsFieldInfo => _o is FieldInfo ? this : null;

        EventInfo IExtEventInfo.EventInfo => (EventInfo)_o;

        public IExtEventInfo? AsEventInfo => _o is EventInfo ? this : null;

        public string Name => _name;

        public Type Type => _type;

        public Type DeclaringType => _declaringType;

        public IReadOnlyList<CustomAttributeData> CustomAttributesData
        {
            get
            {
                return _customAttributesData ??= _o switch
                    {
                        MemberInfo m => m.GetCustomAttributesData().ToArray(),
                        ParameterInfo p => p.GetCustomAttributesData().ToArray(),
                        _ => Throw.NotSupportedException<CustomAttributeData[]>(),
                    };
            }
        }

        public IReadOnlyList<object> CustomAttributes
        {
            get
            {
                return _customAttributes ??= _o.GetCustomAttributes( false );
            }
        }

        public IEnumerable<T> GetCustomAttributes<T>() => CustomAttributes.OfType<T>();

        public IExtNullabilityInfo? GetHomogeneousNullabilityInfo( IActivityMonitor monitor )
        {
            if( !ReadNullabilityInfo.IsHomogeneous )
            {
                monitor.Error( $"Read/Write nullabilities differ for {ToString()}. No [AllowNull], [DisallowNull] or other nullability attributes should be used." );
                return null;
            }
            return _rNullabilityInfo;
        }

        public string TypeCSharpName => _typeName ??= _type.ToCSharpName();

        public IExtNullabilityInfo? HomogeneousNullabilityInfo => ReadNullabilityInfo.IsHomogeneous ? _rNullabilityInfo : null;

        public IExtNullabilityInfo ReadNullabilityInfo
        {
            get
            {
                if( _rNullabilityInfo == null )
                {
                    _rNullabilityInfo = _factory.CreateNullabilityInfo( this, true );
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
                    _wNullabilityInfo = _factory.CreateNullabilityInfo( this, false );
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
}
