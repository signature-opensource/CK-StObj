using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordType CreateRecord( IActivityMonitor monitor,
                                                 PocoTypeSystem s,
                                                 StringCodeWriter sharedWriter,
                                                 Type tNotNull,
                                                 Type tNull,
                                                 string typeName,
                                                 bool isAnonymous,
                                                 RecordField[] fields )
        {
            return new RecordType( monitor, s, sharedWriter, tNotNull, tNull, typeName, isAnonymous, fields );
        }

        internal static RecordType CreateNakedRecord( IActivityMonitor monitor,
                                                      PocoTypeSystem s,
                                                      ICompositePocoType c,
                                                      Type? tNotNull,
                                                      Type? tNull )
        {
            Debug.Assert( c.NakedRecord == null, "We are building the naked record." );
            // If we come from a ValueTuple, then the Types are known.
            // When coming from Poco, we must synthesize the ValueTuple and its nullable. 
            if( tNotNull == null )
            {
                tNotNull = CreateValueTuple( c.Fields, 0 );
                tNull = typeof( Nullable<> ).MakeGenericType( tNotNull );
            }
            Debug.Assert( tNull != null );
            var fields = new RecordField[c.Fields.Count];
            var b = new StringBuilder();
            b.Append( '(' );
            foreach( var f in c.Fields )
            {
                if( b.Length != 1 ) b.Append( ',' );
                b.Append( f.Type.CSharpName );
                fields[f.Index] = new RecordField( f );
            }
            b.Append( ')' );
            var r = new RecordType( monitor, s, new StringCodeWriter(), tNotNull, tNull, b.ToString(), true, fields );
            r.SetNakedRecord( r );
            return r;
        }

        static Type CreateValueTuple( IReadOnlyList<IPocoField> fields, int offset )
        {
            return (fields.Count - offset) switch
            {
                1 => typeof( ValueTuple<> ).MakeGenericType( fields[offset].Type.Type ),
                2 => typeof( ValueTuple<,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type ),
                3 => typeof( ValueTuple<,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type ),
                4 => typeof( ValueTuple<,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type ),
                5 => typeof( ValueTuple<,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type ),
                6 => typeof( ValueTuple<,,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type, fields[offset + 5].Type.Type ),
                7 => typeof( ValueTuple<,,,,,,> ).MakeGenericType( fields[offset].Type.Type, fields[offset + 1].Type.Type, fields[offset + 2].Type.Type, fields[offset + 3].Type.Type, fields[offset + 4].Type.Type, fields[offset + 5].Type.Type, fields[offset + 6].Type.Type ),
                >= 8 => typeof( ValueTuple<,,,,,,,> ).MakeGenericType( fields[offset].Type.Type,
                                                                       fields[offset + 1].Type.Type,
                                                                       fields[offset + 2].Type.Type,
                                                                       fields[offset + 3].Type.Type,
                                                                       fields[offset + 4].Type.Type,
                                                                       fields[offset + 5].Type.Type,
                                                                       fields[offset + 6].Type.Type,
                                                                       CreateValueTuple( fields, offset + 7 ) )
            };
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

                public IRecordPocoType NakedRecord => NonNullable._naked.Nullable;

                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                IRecordPocoType IRecordPocoType.Nullable => this;

                ICompositePocoType ICompositePocoType.Nullable => this;

                public bool RequiresInit => false;
            }

            readonly RecordField[] _fields;
            [AllowNull]
            IRecordPocoType _naked;
            readonly DefaultValueInfo _defInfo;

            public RecordType( IActivityMonitor monitor,
                               PocoTypeSystem s,
                               StringCodeWriter sharedWriter,
                               Type tNotNull,
                               Type tNull,
                               string typeName,
                               bool isAnonymous,
                               RecordField[] fields )
                : base( s,
                        tNotNull,
                        typeName,
                        isAnonymous ? PocoTypeKind.AnonymousRecord : PocoTypeKind.Record,
                        t => new Null( t, tNull ) )
            {
                _fields = fields;
                foreach( var f in fields ) f.SetOwner( this );
                _defInfo = CompositeHelper.CreateDefaultValueInfo( monitor, sharedWriter.StringBuilder, this );
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            public IRecordPocoType NakedRecord => _naked;

            internal void SetNakedRecord( IRecordPocoType r )
            {
                _naked = r;
            }

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            public bool IsAnonymous => Kind == PocoTypeKind.AnonymousRecord;

            IRecordPocoType IRecordPocoType.Nullable => Nullable;

            IRecordPocoType IRecordPocoType.NonNullable => this;

        }

    }
}
