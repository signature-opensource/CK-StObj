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
        readonly PocoFieldAccessKind _fieldAccesskind;

        public PrimaryPocoField( IPocoPropertyInfo p,
                                 IPocoType type,
                                 string fieldTypeName,
                                 PocoFieldAccessKind fieldAccesskind,
                                 PocoType.PrimaryPocoType owner,
                                 IPocoFieldDefaultValue? defaultValue )
        {
            _p = p;
            _type = type;
            _defInfo = defaultValue != null ? new DefaultValueInfo( defaultValue ) : type.DefaultValueInfo;
            _privateFieldName = $"_v{Index}";
            _fieldAccesskind = fieldAccesskind;
            FieldTypeCSharpName = fieldTypeName;
            _owner = owner;
            if( type.Kind != PocoTypeKind.Any )
            {
                _nextRef = ((PocoType)type.NonNullable).AddBackRef( this );
            }
        }

        public IPrimaryPocoType Owner => _owner;

        ICompositePocoType IPocoField.Owner => _owner;

        IPocoType IPocoType.ITypeRef.Owner => _owner;

        public bool IsExchangeable => _fieldAccesskind != PocoFieldAccessKind.ReadOnly && _type.IsExchangeable;

        public int Index => _p.Index;

        public string Name => _p.Name;

        public IPocoPropertyInfo Property => _p;

        public string PrivateFieldName => _privateFieldName;

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public PocoFieldAccessKind FieldAccess => _fieldAccesskind;

        public string FieldTypeCSharpName { get; }

        IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

        public override string ToString() => $"{_type} {Name}";
    }
}
