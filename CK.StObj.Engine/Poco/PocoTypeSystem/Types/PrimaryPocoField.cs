using CK.Core;
using System;

namespace CK.Setup
{
    sealed class PrimaryPocoField : IPrimaryPocoField
    {
        readonly IPocoPropertyInfo _p;
        readonly IPocoType _type;
        readonly PocoType.PrimaryPocoType _owner;
        readonly IPocoType.ITypeRef? _nextRef;
        readonly string _privateFieldName;

        readonly DefaultValueInfo _defInfo;
        readonly bool _hasSetter;
        readonly bool _isByRef;

        public PrimaryPocoField( IPocoPropertyInfo p,
                                 IPocoType type,
                                 string fieldTypeName,
                                 bool hasSetter,
                                 PocoType.PrimaryPocoType owner,
                                 bool isByRef,
                                 IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            _defInfo = defaultValue != null ? new DefaultValueInfo( defaultValue ) : type.DefaultValueInfo;
            _privateFieldName = $"_v{Index}";
            _hasSetter = hasSetter;
            FieldTypeCSharpName = fieldTypeName;
            _owner = owner;
            _isByRef = isByRef;
            _nextRef = ((PocoType)type.NonNullable).AddBackRef( this );
        }

        public IPrimaryPocoType Owner => _owner;

        ICompositePocoType IPocoField.Owner => _owner;

        IPocoType IPocoType.ITypeRef.Owner => _owner;

        public bool IsExchangeable => (_hasSetter || _isByRef) && _type.IsExchangeable;

        public int Index => _p.Index;

        public string Name => _p.Name;

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName => _privateFieldName;

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public bool HasSetter => _hasSetter;

        public bool IsByRef => _isByRef;

        public string FieldTypeCSharpName { get; }

        IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

        public override string ToString() => $"{_type} {Name}";
    }
}
