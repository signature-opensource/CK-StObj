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

        internal static RecordAnonType CreateAnonymousRecord( IActivityMonitor monitor,
                                                              PocoTypeSystemBuilder s,
                                                              Type tNotNull,
                                                              Type tNull,
                                                              string typeName,
                                                              RecordAnonField[] fields,
                                                              bool isReadOnlyCompliant,
                                                              IPocoType? obliviousType )
        {
            return new RecordAnonType( monitor, s, tNotNull, tNull, typeName, fields, isReadOnlyCompliant, (IRecordPocoType?)obliviousType );
        }

        internal sealed class RecordAnonType : PocoType, IRecordPocoType
        {
            sealed class Null : NullValueType, IRecordPocoType
            {
                public Null( IPocoType notNullable, Type tNull )
                    : base( notNullable, tNull )
                {
                }

                new RecordAnonType NonNullable => Unsafe.As<RecordAnonType>( base.NonNullable );

                public bool IsAnonymous => true;

                public override IPocoType ObliviousType => NonNullable.ObliviousType.Nullable;

                public bool IsReadOnlyCompliant => NonNullable.IsReadOnlyCompliant;

                ICompositePocoType ICompositePocoType.ObliviousType => Unsafe.As<ICompositePocoType>( ObliviousType );

                IRecordPocoType IRecordPocoType.ObliviousType => Unsafe.As<IRecordPocoType>( ObliviousType );

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IRecordPocoType IRecordPocoType.Nullable => this;
                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;
                ICompositePocoType ICompositePocoType.Nullable => this;

                INamedPocoType INamedPocoType.Nullable => this;
                INamedPocoType INamedPocoType.NonNullable => NonNullable;

                public ExternalNameAttribute? ExternalName => null;

                // No external name for anonymous records: simply use the nullable CSharpName.
                public string ExternalOrCSharpName => CSharpName;
            }

            readonly RecordAnonField[] _fields;
            readonly IRecordPocoType _obliviousType;
            readonly DefaultValueInfo _defInfo;
            readonly bool _isReadOnlyCompliant;

            public RecordAnonType( IActivityMonitor monitor,
                                   PocoTypeSystemBuilder s,
                                   Type tNotNull,
                                   Type tNull,
                                   string typeName,
                                   RecordAnonField[] fields,
                                   bool isReadOnlyCompliant,
                                   IRecordPocoType? obliviousType )
                : base( s,
                        tNotNull,
                        typeName,
                        PocoTypeKind.AnonymousRecord,
                        t => new Null( t, tNull ) )
            {
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.Fields.All( f => f.IsUnnamed && f.Type.IsOblivious ) );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    Throw.DebugAssert( fields.All( f => f.IsUnnamed && f.Type.IsOblivious ) );
                    _obliviousType = this;
                }
                _fields = fields;
                _isReadOnlyCompliant = isReadOnlyCompliant;
                foreach( var f in fields ) f.SetOwner( this );
                _defInfo = CompositeHelper.CreateDefaultValueInfo( s.StringBuilderPool, this );
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public ExternalNameAttribute? ExternalName => null;

            public string ExternalOrCSharpName => CSharpName;

            public bool IsReadOnlyCompliant => _isReadOnlyCompliant;

            public override IPocoType ObliviousType => _obliviousType;

            ICompositePocoType ICompositePocoType.ObliviousType => _obliviousType;

            IRecordPocoType IRecordPocoType.ObliviousType => _obliviousType;

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            public bool IsAnonymous => Kind == PocoTypeKind.AnonymousRecord;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;
            ICompositePocoType ICompositePocoType.NonNullable => this;

            IRecordPocoType IRecordPocoType.Nullable => Nullable;
            IRecordPocoType IRecordPocoType.NonNullable => this;

            INamedPocoType INamedPocoType.Nullable => Nullable;
            INamedPocoType INamedPocoType.NonNullable => this;

            public override bool CanReadFrom( IPocoType type )
            {
                // type.IsNullable may be true: we don't care.
                if( type.NonNullable == this || type.Kind == PocoTypeKind.Any ) return true;
                if( type.Kind != PocoTypeKind.AnonymousRecord ) return false;
                var aType = (RecordAnonType)type.NonNullable;
                if( _fields.Length != aType._fields.Length ) return false;
                for( int i = 0; i < _fields.Length; i++ )
                {
                    if( !_fields[i].Type.CanReadFrom( aType._fields[i].Type ) ) return false;
                }
                return true;
            }
        }

    }
}
