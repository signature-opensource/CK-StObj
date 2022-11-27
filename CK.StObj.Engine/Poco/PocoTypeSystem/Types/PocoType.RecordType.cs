using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
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
                                                 RecordField[]? anonymousFields,
                                                 ExternalNameAttribute? externalName,
                                                 IPocoType? obliviousType )
        {
            return new RecordType( monitor, s, tNotNull, tNull, typeName, anonymousFields, externalName, (IRecordPocoType?)obliviousType );
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

                public override IPocoType ObliviousType => NonNullable.ObliviousType.Nullable;

                ICompositePocoType ICompositePocoType.ObliviousType => Unsafe.As<ICompositePocoType>( ObliviousType );

                IRecordPocoType IRecordPocoType.ObliviousType => Unsafe.As<IRecordPocoType>( ObliviousType );

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                IRecordPocoType IRecordPocoType.Nullable => this;

                ICompositePocoType ICompositePocoType.Nullable => this;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => NonNullable.ExternalOrCSharpName;
            }

            [AllowNull] RecordField[] _fields;
            [AllowNull] IRecordPocoType _obliviousType;
            readonly ExternalNameAttribute? _externalName;
            DefaultValueInfo _defInfo;

            public RecordType( IActivityMonitor monitor,
                               PocoTypeSystem s,
                               Type tNotNull,
                               Type tNull,
                               string typeName,
                               RecordField[]? anonymousFields,
                               ExternalNameAttribute? externalName,
                               IRecordPocoType? obliviousType )
                : base( s,
                        tNotNull,
                        typeName,
                        anonymousFields != null ? PocoTypeKind.AnonymousRecord : PocoTypeKind.Record,
                        t => new Null( t, tNull ) )
            {
                if( anonymousFields != null )
                {
                    SetFields( monitor, s, anonymousFields, obliviousType );
                }
                _externalName = externalName;
            }

            internal void SetFields( IActivityMonitor monitor, PocoTypeSystem s, RecordField[] fields, IRecordPocoType? obliviousType )
            {
                _fields = fields;
                foreach( var f in fields ) f.SetOwner( this );
                _defInfo = CompositeHelper.CreateDefaultValueInfo( monitor, s.StringBuilderPool, this );
                _obliviousType = obliviousType ?? this;
                // Sets the initial IsExchangeable status.
                if( !_fields.Any( f => f.IsExchangeable ) )
                {
                    SetNotExchangeable( monitor, $"none of its {_fields.Length} fields are exchangeable." );
                }
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public ExternalNameAttribute? ExternalName => _externalName;

            public string ExternalOrCSharpName => _externalName?.Name ?? CSharpName;

            public override IPocoType ObliviousType => _obliviousType;

            ICompositePocoType ICompositePocoType.ObliviousType => _obliviousType;

            IRecordPocoType IRecordPocoType.ObliviousType => _obliviousType;

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
