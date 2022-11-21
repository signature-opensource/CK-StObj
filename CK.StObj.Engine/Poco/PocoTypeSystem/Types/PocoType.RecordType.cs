using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using static CK.CodeGen.TupleTypeName;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordType CreateRecord( IActivityMonitor monitor,
                                                 PocoTypeSystem s,
                                                 Type tNotNull,
                                                 Type tNull,
                                                 string typeName,
                                                 RecordField[]? anonymousFields )
        {
            return new RecordType( monitor, s, tNotNull, tNull, typeName, anonymousFields );
        }

        internal sealed class RecordType : PocoType, IRecordPocoType
        {
            sealed class Null : NullValueType, IRecordPocoType
            {
                public Null( IPocoType notNullable, Type tNull )
                    : base( notNullable, tNull )
                {
                }

                new RecordType NonNullable => Unsafe.As<RecordType>( base.NonNullable );

                public bool IsAnonymous => NonNullable.IsAnonymous;

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                IRecordPocoType IRecordPocoType.Nullable => this;

                ICompositePocoType ICompositePocoType.Nullable => this;

            }

            [AllowNull] RecordField[] _fields;
            DefaultValueInfo _defInfo;

            public RecordType( IActivityMonitor monitor,
                               PocoTypeSystem s,
                               Type tNotNull,
                               Type tNull,
                               string typeName,
                               RecordField[]? anonymousFields )
                : base( s,
                        tNotNull,
                        typeName,
                        anonymousFields != null ? PocoTypeKind.AnonymousRecord : PocoTypeKind.Record,
                        t => new Null( t, tNull ) )
            {
                if( anonymousFields != null ) SetFields( monitor, s, anonymousFields );
            }

            internal void SetFields( IActivityMonitor monitor, PocoTypeSystem s, RecordField[] fields )
            {
                _fields = fields;
                foreach( var f in fields ) f.SetOwner( this );
                _defInfo = CompositeHelper.CreateDefaultValueInfo( monitor, s.StringBuilderPool, this );
                // Sets the initial IsExchangeable status.
                if( !_fields.Any( f => f.IsExchangeable ) )
                {
                    SetNotExchangeable( monitor, $"none of its {_fields.Length} fields are exchangeable." );
                }
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            public bool IsAnonymous => Kind == PocoTypeKind.AnonymousRecord;

            IRecordPocoType IRecordPocoType.Nullable => Nullable;

            IRecordPocoType IRecordPocoType.NonNullable => this;

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                Debug.Assert( r != null && _fields.Any( f => f == r ) && !r.Type.IsExchangeable );
                if( IsExchangeable )
                {
                    if( !_fields.Any( f => f.IsExchangeable ) )
                    {
                        SetNotExchangeable( monitor, $"its last field type '{r.Type}' is not exchangeable." );
                    }
                }
            }
        }

    }
}
