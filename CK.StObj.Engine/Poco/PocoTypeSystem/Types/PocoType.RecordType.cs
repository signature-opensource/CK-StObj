using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordType CreateRecord( PocoTypeSystem s,
                                                 Type tNotNull,
                                                 Type tNull,
                                                 string typeName,
                                                 bool isAnonymous,
                                                 RecordField[] fields )
        {
            return new RecordType( s, tNotNull, tNull, typeName, isAnonymous ? PocoTypeKind.AnonymousRecord : PocoTypeKind.Record, fields );
        }

        internal sealed class RecordType : PocoType, IRecordPocoType
        {
            public RecordType( PocoTypeSystem s, Type tNotNull, Type tNull, string typeName, PocoTypeKind typeKind, RecordField[] fields )
                : base( s, tNotNull, typeName, typeKind, t => new Null( t, tNull ) )
            {
                Fields = fields;
                RequiresInit = fields.Any( f => f.DefaultValue != null || (f.Type.Kind != PocoTypeKind.Basic && !f.Type.IsNullable) );
            }

            sealed class Null : NullBasicWithType, IRecordPocoType
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

                public bool RequiresInit => false;
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public IReadOnlyList<IRecordPocoField> Fields { get; }

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => Fields;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            public bool IsAnonymous => Kind == PocoTypeKind.AnonymousRecord;

            public bool RequiresInit { get; }

            IRecordPocoType IRecordPocoType.Nullable => Nullable;

            IRecordPocoType IRecordPocoType.NonNullable => this;
        }
    }
}
