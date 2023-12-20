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
using static CK.Setup.IPocoType;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordNamedType CreateNamedRecord( IActivityMonitor monitor,
                                                           PocoTypeSystem s,
                                                           Type tNotNull,
                                                           Type tNull,
                                                           string typeName,
                                                           ExternalNameAttribute? externalName )
        {
            return new RecordNamedType( monitor, s, tNotNull, tNull, typeName, externalName );
        }

        internal sealed class RecordNamedType : PocoType, IRecordPocoType
        {
            sealed class Null : NullValueType, IRecordPocoType
            {
                public Null( IPocoType notNullable, Type tNull )
                    : base( notNullable, tNull )
                {
                }

                new RecordNamedType NonNullable => Unsafe.As<RecordNamedType>( base.NonNullable );

                public bool IsAnonymous => NonNullable.IsAnonymous;

                ICompositePocoType ICompositePocoType.ObliviousType => Unsafe.As<ICompositePocoType>( this );

                IRecordPocoType IRecordPocoType.ObliviousType => Unsafe.As<IRecordPocoType>( this );

                public bool IsReadOnlyCompliant => NonNullable.IsReadOnlyCompliant;

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                IRecordPocoType IRecordPocoType.Nullable => this;

                ICompositePocoType ICompositePocoType.Nullable => this;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => NonNullable.ExternalOrCSharpName;
            }

            [AllowNull] RecordNamedField[] _fields;
            readonly ExternalNameAttribute? _externalName;
            DefaultValueInfo _defInfo;
            bool _isReadOnlyCompliant;

            public RecordNamedType( IActivityMonitor monitor,
                                    PocoTypeSystem s,
                                    Type tNotNull,
                                    Type tNull,
                                    string typeName,
                                    ExternalNameAttribute? externalName )
                : base( s,
                        tNotNull,
                        typeName,
                        PocoTypeKind.Record,
                        t => new Null( t, tNull ) )
            {
                _externalName = externalName;
            }

            internal void SetFields( IActivityMonitor monitor, PocoTypeSystem s, bool isReadOnlyCompliant, RecordNamedField[] fields )
            {
                _fields = fields;
                _defInfo = CompositeHelper.CreateDefaultValueInfo( monitor, s.StringBuilderPool, this );
                _isReadOnlyCompliant = isReadOnlyCompliant;
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

            public bool IsReadOnlyCompliant => _isReadOnlyCompliant;

            ICompositePocoType ICompositePocoType.ObliviousType => this;

            IRecordPocoType IRecordPocoType.ObliviousType => this;

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;

            ICompositePocoType ICompositePocoType.NonNullable => this;

            public bool IsAnonymous => false;

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
