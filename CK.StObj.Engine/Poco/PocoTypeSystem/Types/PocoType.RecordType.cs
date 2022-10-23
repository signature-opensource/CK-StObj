using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

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

                public bool RequiresInit => false;
            }

            readonly RecordField[] _fields;
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
                _defInfo = CompositeHelper.CreateDefaultValueInfo( monitor, sharedWriter, this );
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
        }

    }
}
