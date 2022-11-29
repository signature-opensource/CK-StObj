using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CK.Setup
{
    sealed class RecordAnonField : IRecordPocoField
    {
        readonly static List<string> _itemNames = new List<string>();

        static string GetItemName( int index )
        {
            while( index >= _itemNames.Count ) _itemNames.Add( $"Item{_itemNames.Count + 1}" );
            return _itemNames[index];
        }

        [AllowNull] IPocoType _type;
        [AllowNull] PocoType.RecordAnonType _owner;
        IPocoType.ITypeRef? _nextRef;
        readonly string _name;
        readonly int _index;
        readonly bool _isUnnamed;

        // Used to build the oblivious type fields.
        internal RecordAnonField( RecordAnonField f )
        {
            _index = f._index;
            _name = f._name;
            _isUnnamed = f._isUnnamed;
            SetType( f.Type.ObliviousType );
        }

        public RecordAnonField( int index, string? name )
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
        }

        public int Index => _index;

        public string Name => _name;

        public bool IsUnnamed => _isUnnamed;

        public IPocoType Type => _type;

        IPocoType IPocoType.ITypeRef.Type => _type;

        public bool IsExchangeable => _type.IsExchangeable;

        public DefaultValueInfo DefaultValueInfo => _type.DefaultValueInfo;

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
        }

        internal void SetOwner( PocoType.RecordAnonType record ) => _owner = record;

        public override string ToString() => $"{(_type == null ? "(no type)" : _type)} {Name}";

    }

}
