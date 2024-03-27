using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordNamedType CreateNamedRecord( IActivityMonitor monitor,
                                                           PocoTypeSystemBuilder s,
                                                           Type tNotNull,
                                                           Type tNull,
                                                           string typeName,
                                                           ExternalNameAttribute? externalName )
        {
            return new RecordNamedType( s, tNotNull, tNull, typeName, externalName );
        }

        internal sealed class RecordNamedType : PocoType, IRecordPocoType
        {
            sealed class Null : NullValueType, IRecordPocoType
            {
                [AllowNull] internal string _extOrCSName;

                public Null( IPocoType notNullable, Type tNull )
                    : base( notNullable, tNull )
                {
                }

                new RecordNamedType NonNullable => Unsafe.As<RecordNamedType>( base.NonNullable );

                public bool IsAnonymous => NonNullable.IsAnonymous;

                public override IRecordPocoType ObliviousType => this;
                // Required... C# "Covariant return type" cand do better...
                ICompositePocoType ICompositePocoType.ObliviousType => this;

                public override IRecordPocoType RegularType => this;

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IRecordPocoType IRecordPocoType.Nullable => this;
                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.Nullable => this;
                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;

                INamedPocoType INamedPocoType.Nullable => this;
                INamedPocoType INamedPocoType.NonNullable => NonNullable;

                public ExternalNameAttribute? ExternalName => NonNullable.ExternalName;

                public string ExternalOrCSharpName => _extOrCSName;
            }

            [AllowNull] RecordNamedField[] _fields;
            readonly ExternalNameAttribute? _externalName;
            DefaultValueInfo _defInfo;
            bool _isReadOnlyCompliant;

            public RecordNamedType( PocoTypeSystemBuilder s,
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
                if( externalName != null )
                {
                    _externalName = externalName;
                    Nullable._extOrCSName = externalName.Name + '?';
                }
                else
                {
                    Nullable._extOrCSName = Nullable.CSharpName;
                }
            }

            internal void SetFields( IActivityMonitor monitor, PocoTypeSystemBuilder s, bool isReadOnlyCompliant, RecordNamedField[] fields )
            {
                _fields = fields;
                _defInfo = CompositeHelper.CreateDefaultValueInfo( s.StringBuilderPool, this );
                _isReadOnlyCompliant = isReadOnlyCompliant;
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public ExternalNameAttribute? ExternalName => _externalName;

            public string ExternalOrCSharpName => _externalName?.Name ?? CSharpName;

            public override bool IsReadOnlyCompliant => _isReadOnlyCompliant;

            public override IRecordPocoType ObliviousType => this;
            // Required... C# "Covariant return type" can do better.
            ICompositePocoType ICompositePocoType.ObliviousType => this;

            public override IRecordPocoType RegularType => this;

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            public bool IsAnonymous => false;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;
            ICompositePocoType ICompositePocoType.NonNullable => this;

            IRecordPocoType IRecordPocoType.Nullable => Nullable;
            IRecordPocoType IRecordPocoType.NonNullable => this;

            INamedPocoType INamedPocoType.Nullable => Nullable;
            INamedPocoType INamedPocoType.NonNullable => this;
        }
    }
}
