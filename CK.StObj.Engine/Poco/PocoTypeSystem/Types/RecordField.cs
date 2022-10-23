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
        [AllowNull]
        IPocoType _type;
        [AllowNull]
        PocoType.RecordType _owner;
        DefaultValueInfo _defInfo;

        public RecordField( int index, string? name, IPocoFieldDefaultValue? defaultValue = null )
        {
            Index = index;
            if( name == null )
            {
                Name = $"Item{index + 1}";
                IsUnnamed = true;
            }
            else
            {
                Name = name;
            }
            if( defaultValue != null )
            {
                _defInfo = new DefaultValueInfo( defaultValue );
            }
        }

        public int Index { get; }

        public string Name { get; }

        public bool IsUnnamed { get; }

        public IPocoType Type => _type;

        public DefaultValueInfo DefaultValueInfo => _defInfo;

        public ICompositePocoType Owner => _owner;

        internal void SetType( IPocoType t )
        {
            Debug.Assert( _type == null && t != null );
            _type = t;
            if( _defInfo.IsDisallowed )
            {
                _defInfo = _type.DefaultValueInfo;
            }
        }

        internal void SetOwner( PocoType.RecordType record )
        {
            _owner = record;
        }

        public override string ToString() => $"{(_type == null ? "(no type)" : _type)} {Name}";

    }
}
