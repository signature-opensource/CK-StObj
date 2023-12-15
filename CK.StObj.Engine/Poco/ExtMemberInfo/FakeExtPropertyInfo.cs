using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    sealed class FakeExtPropertyInfo : IExtPropertyInfo
    {
        readonly FakeProp _prop;
        readonly object?[] _customAttributes;
        [AllowNull] IPocoPropertyInfo _pocoProp;
        [AllowNull] IExtNullabilityInfo _nullabilityInfo;
        [AllowNull] string _typeCSharpName;

        public FakeExtPropertyInfo()
        {
            _prop = new FakeProp( this );
            _customAttributes = new object[1];
        }

        public void SetInfo( IPocoPropertyInfo pocoProp,
                             IExtNullabilityInfo type,
                             TupleElementNamesAttribute? valueTupleAttr )
        {
            Throw.DebugAssert( type != null && type.IsHomogeneous );
            _pocoProp = pocoProp;
            _nullabilityInfo = type;
            _typeCSharpName = type.Type.ToCSharpName();
            _customAttributes[0] = valueTupleAttr;
        }

        sealed class FakeProp : PropertyInfo
        {
            readonly FakeExtPropertyInfo _p;

            public FakeProp( FakeExtPropertyInfo p )
            {
                _p = p;
            }

            public override PropertyAttributes Attributes => _p._pocoProp.DeclaredProperties[0].PropertyInfo.Attributes;

            public override bool CanRead => false;

            public override bool CanWrite => false;

            public override Type PropertyType => _p.Type;

            public override Type? DeclaringType => _p.DeclaringType;

            public override string Name => _p.Name;

            public override Type? ReflectedType => null;

            public override MethodInfo[] GetAccessors( bool nonPublic )
            {
                throw new NotSupportedException();
            }

            public override object[] GetCustomAttributes( bool inherit )
            {
                throw new NotSupportedException();
            }

            public override object[] GetCustomAttributes( Type attributeType, bool inherit )
            {
                throw new NotSupportedException();
            }

            public override MethodInfo? GetGetMethod( bool nonPublic )
            {
                throw new NotSupportedException();
            }

            public override ParameterInfo[] GetIndexParameters()
            {
                throw new NotSupportedException();
            }

            public override MethodInfo? GetSetMethod( bool nonPublic )
            {
                throw new NotSupportedException();
            }

            public override object? GetValue( object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture )
            {
                throw new NotSupportedException();
            }

            public override bool IsDefined( Type attributeType, bool inherit )
            {
                throw new NotSupportedException();
            }

            public override void SetValue( object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture )
            {
                throw new NotSupportedException();
            }
        }

        public PropertyInfo PropertyInfo => _prop;

        public Type DeclaringType => _pocoProp.DeclaredProperties[0].Type;

        public Type Type => _nullabilityInfo.Type;

        public string Name => _pocoProp.Name;

        public string TypeCSharpName => _typeCSharpName;

        public IReadOnlyList<CustomAttributeData> CustomAttributesData => Array.Empty<CustomAttributeData>();

        public IReadOnlyList<object> CustomAttributes => (_customAttributes[0] != null ? _customAttributes : Array.Empty<object>())!;

        public IExtNullabilityInfo? HomogeneousNullabilityInfo => _nullabilityInfo;

        public IExtNullabilityInfo ReadNullabilityInfo => _nullabilityInfo;

        public IExtNullabilityInfo WriteNullabilityInfo => _nullabilityInfo;

        public override string ToString() => _pocoProp.ToString()!;
    }
}
