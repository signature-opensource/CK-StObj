using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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

        // Used to build the oblivious or unnamed type fields.
        internal RecordAnonField( RecordAnonField f, bool isOblivious )
        {
            _index = f._index;
            _name = GetItemName( _index );
            _isUnnamed = true;
            SetType( isOblivious ? f.Type.ObliviousType : f.Type );
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

        public object? Originator => null;

        public DefaultValueInfo DefaultValueInfo => _type.DefaultValueInfo;

        public bool HasOwnDefaultValue => false;

        public ICompositePocoType Owner => _owner;

        IPocoType IPocoType.ITypeRef.Owner => _owner;

        IPocoType.ITypeRef? IPocoType.ITypeRef.NextRef => _nextRef;

        internal void SetType( IPocoType t )
        {
            // We cannot register the backref here because we don't
            // know yet if the record that owns this field is not
            // already registered.
            Throw.DebugAssert( _type == null && t != null );
            _type = t;
        }

        internal void SetOwner( PocoType.RecordAnonType record )
        {
            Throw.DebugAssert( _type != null );
            Throw.DebugAssert( _owner == null && record != null && !record.IsNullable );
            _owner = record;
            _nextRef = ((PocoType)_type.NonNullable).AddBackRef( this );
        }

        public override string ToString() => $"{(_type == null ? "(no type)" : _type)} {Name}";

    }

}
