using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    sealed class RecordNamedField : IRecordPocoField
    {
        [AllowNull] IPocoType _type;
        [AllowNull] IRecordPocoType _owner;
        IPocoType.ITypeRef? _nextRef;
        readonly string _name;
        readonly int _index;
        readonly DefaultValueInfo _defInfo;

        public RecordNamedField( IRecordPocoType record, int index, string name, IPocoType t, IPocoFieldDefaultValue? defaultValue = null )
        {
            _index = index;
            _name = name;
            _type = t;
            _owner = record;
            if( t.Kind != PocoTypeKind.Any )
            {
                _nextRef = ((PocoType)t.NonNullable).AddBackRef( this );
            }
            _defInfo = defaultValue != null
                        ? new DefaultValueInfo( defaultValue )
                        : _type.DefaultValueInfo;
        }

        public int Index => _index;

        public string Name => _name;

        public bool IsUnnamed => false;

        public IPocoType Type => _type;

        IPocoType IPocoType.ITypeRef.Type => _type;

        public bool IsExchangeable => _type.IsExchangeable;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public ICompositePocoType Owner => _owner;

        IPocoType IPocoType.ITypeRef.Owner => _owner;

        IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

        public override string ToString() => $"{_type} {Name}";
    }

}
