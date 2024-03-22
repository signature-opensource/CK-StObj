using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {

        internal static RecordAnonType CreateAnonymousRecord( PocoTypeSystemBuilder s,
                                                              Type tNotNull,
                                                              Type tNull,
                                                              string typeName,
                                                              RecordAnonField[] fields,
                                                              bool isReadOnlyCompliant,
                                                              IPocoType? unnamedRecord,
                                                              IPocoType? obliviousType )
        {
            return new RecordAnonType( s,
                                       tNotNull,
                                       tNull,
                                       typeName,
                                       fields,
                                       isReadOnlyCompliant,
                                       (IAnonymousRecordPocoType?)unnamedRecord,
                                       (IAnonymousRecordPocoType?)obliviousType );
        }

        internal sealed class RecordAnonType : PocoType, IAnonymousRecordPocoType
        {
            sealed class Null : NullValueType, IAnonymousRecordPocoType
            {
                public Null( IPocoType notNullable, Type tNull )
                    : base( notNullable, tNull )
                {
                }

                new RecordAnonType NonNullable => Unsafe.As<RecordAnonType>( base.NonNullable );

                public bool IsAnonymous => true;

                public override IAnonymousRecordPocoType ObliviousType => NonNullable.ObliviousType.Nullable;
                ICompositePocoType ICompositePocoType.ObliviousType => NonNullable.ObliviousType.Nullable;
                IRecordPocoType IRecordPocoType.ObliviousType => NonNullable.ObliviousType.Nullable;

                public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

                IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;

                IAnonymousRecordPocoType IAnonymousRecordPocoType.Nullable => this;
                IAnonymousRecordPocoType IAnonymousRecordPocoType.NonNullable => NonNullable;

                IRecordPocoType IRecordPocoType.Nullable => this;
                IRecordPocoType IRecordPocoType.NonNullable => NonNullable;

                ICompositePocoType ICompositePocoType.NonNullable => NonNullable;
                ICompositePocoType ICompositePocoType.Nullable => this;

                INamedPocoType INamedPocoType.Nullable => this;
                INamedPocoType INamedPocoType.NonNullable => NonNullable;

                public ExternalNameAttribute? ExternalName => null;

                // No external name for anonymous records: simply use the nullable CSharpName.
                public string ExternalOrCSharpName => CSharpName;

                public bool IsUnnamed => NonNullable.IsUnnamed;

                public IAnonymousRecordPocoType UnnamedRecord => NonNullable.UnnamedRecord.Nullable;
            }

            readonly RecordAnonField[] _fields;
            readonly IAnonymousRecordPocoType _obliviousType;
            readonly IAnonymousRecordPocoType _unnamedRecord;
            readonly DefaultValueInfo _defInfo;
            readonly bool _isReadOnlyCompliant;

            public RecordAnonType( PocoTypeSystemBuilder s,
                                   Type tNotNull,
                                   Type tNull,
                                   string typeName,
                                   RecordAnonField[] fields,
                                   bool isReadOnlyCompliant,
                                   IAnonymousRecordPocoType? unnamedRecord,
                                   IAnonymousRecordPocoType? obliviousType )
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
                    // Registers the back reference to the unnamed if there is an unnamed or
                    // to the oblivious type if we are the unnamed.
                    _ = new PocoTypeRef( this, unnamedRecord ?? obliviousType, -1 );
                }
                else
                {
                    Throw.DebugAssert( fields.All( f => f.IsUnnamed && f.Type.IsOblivious ) );
                    Throw.DebugAssert( "The oblivious is unnamed.", unnamedRecord == null );
                    // For value type, oblivious is the non nullable.
                    _obliviousType = this;
                }
                _fields = fields;
                _isReadOnlyCompliant = isReadOnlyCompliant;
                _unnamedRecord = unnamedRecord ?? this;
                foreach( var f in fields ) f.SetOwner( this );
                _defInfo = CompositeHelper.CreateDefaultValueInfo( s.StringBuilderPool, this );

                Throw.DebugAssert( "An unnamed record has no field name and its subordinated anonymous records are unnamed.",
                                   !IsUnnamed || (_fields.All( f => f.IsUnnamed && (f.Type is not IAnonymousRecordPocoType a || a.IsUnnamed) ) ) );
            }

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public ExternalNameAttribute? ExternalName => null;

            public string ExternalOrCSharpName => CSharpName;

            public override bool IsReadOnlyCompliant => _isReadOnlyCompliant;

            public override IAnonymousRecordPocoType ObliviousType => _obliviousType;
            // Required... C# "Covariant return type" can do better...
            IRecordPocoType IRecordPocoType.ObliviousType => _obliviousType;
            ICompositePocoType ICompositePocoType.ObliviousType => _obliviousType;

            public bool IsUnnamed => _unnamedRecord == this;

            public IAnonymousRecordPocoType UnnamedRecord => _unnamedRecord;

            public IReadOnlyList<IRecordPocoField> Fields => _fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

            public bool IsAnonymous => Kind == PocoTypeKind.AnonymousRecord;

            IAnonymousRecordPocoType IAnonymousRecordPocoType.Nullable => Nullable;
            IAnonymousRecordPocoType IAnonymousRecordPocoType.NonNullable => this;

            IRecordPocoType IRecordPocoType.Nullable => Nullable;
            IRecordPocoType IRecordPocoType.NonNullable => this;

            ICompositePocoType ICompositePocoType.Nullable => Nullable;
            ICompositePocoType ICompositePocoType.NonNullable => this;

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
