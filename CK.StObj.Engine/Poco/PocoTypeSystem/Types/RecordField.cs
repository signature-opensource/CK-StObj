using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    sealed class RecordField : IRecordPocoField
    {
        readonly static List<string> _itemNames = new List<string>();

        [AllowNull] IPocoType _type;
        [AllowNull] PocoType.RecordType _owner;
        IPocoType.ITypeRef? _nextRef;
        readonly string _name;
        readonly int _index;
        DefaultValueInfo _defInfo;
        readonly bool _isUnnamed;

        static string GetItemName( int index )
        {
            while( index >= _itemNames.Count ) _itemNames.Add( $"Item{_itemNames.Count + 1}" );
            return _itemNames[index];
        }

        // Used to build the oblivious type fields.
        internal RecordField( RecordField f )
        {
            _index = f._index;
            _name = f._name;
            _defInfo = f._defInfo;
            _isUnnamed = f._isUnnamed;
            SetType( f.Type.ObliviousType );
        }

        public RecordField( int index, string? name, IPocoFieldDefaultValue? defaultValue = null )
        {
            _index = index;
            if( name == null )
            {
                _name = GetItemName( index );
                _isUnnamed = true;
            }
            else
            {
                _name = name;
            }
            if( defaultValue != null )
            {
                _defInfo = new DefaultValueInfo( defaultValue );
            }
        }

        public int Index => _index;

        public string Name => _name;

        public bool IsUnnamed => _isUnnamed;

        public IPocoType Type => _type;

        public bool IsExchangeable => _type.IsExchangeable;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public ICompositePocoType Owner => _owner;

        IPocoType IPocoType.ITypeRef.Owner => _owner;

        IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

        internal void SetType( IPocoType t )
        {
            Debug.Assert( _type == null && t != null );
            if( t.Kind != PocoTypeKind.Any )
            {
                _nextRef = ((PocoType)t.NonNullable).AddBackRef( this );
            }
            _type = t;
            if( _defInfo.IsDisallowed )
            {
                _defInfo = _type.DefaultValueInfo;
            }
        }

        internal void SetOwner( PocoType.RecordType record ) => _owner = record;

        public override string ToString() => $"{(_type == null ? "(no type)" : _type)} {Name}";

    }
}
