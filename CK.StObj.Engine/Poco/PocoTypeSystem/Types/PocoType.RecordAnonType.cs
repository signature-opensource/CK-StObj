using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup;

partial class PocoType
{

    internal static RecordAnonType CreateAnonymousRecord( PocoTypeSystemBuilder s,
                                                          Type tNotNull,
                                                          Type tNull,
                                                          string typeName,
                                                          RecordAnonField[] fields,
                                                          bool isReadOnlyCompliant,
                                                          IPocoType? regularType,
                                                          IPocoType? obliviousType )
    {
        return new RecordAnonType( s,
                                   tNotNull,
                                   tNull,
                                   typeName,
                                   fields,
                                   isReadOnlyCompliant,
                                   (IRecordPocoType?)regularType,
                                   (IRecordPocoType?)obliviousType );
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

            public override IRecordPocoType ObliviousType => NonNullable.ObliviousType.Nullable;
            ICompositePocoType ICompositePocoType.ObliviousType => NonNullable.ObliviousType.Nullable;

            public override IRecordPocoType RegularType => NonNullable.RegularType.Nullable;

            public IReadOnlyList<IRecordPocoField> Fields => NonNullable.Fields;

            IReadOnlyList<IPocoField> ICompositePocoType.Fields => NonNullable.Fields;
            IReadOnlyList<IBasePocoField> IBaseCompositeType.Fields => NonNullable.Fields;

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
        readonly IRecordPocoType _regularType;
        readonly DefaultValueInfo _defInfo;
        readonly bool _isReadOnlyCompliant;

        public RecordAnonType( PocoTypeSystemBuilder s,
                               Type tNotNull,
                               Type tNull,
                               string typeName,
                               RecordAnonField[] fields,
                               bool isReadOnlyCompliant,
                               IRecordPocoType? regularType,
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
                // Registers the back reference to the unnamed if there is an unnamed or
                // to the oblivious type if we are the unnamed.
                _ = new PocoTypeRef( this, regularType ?? obliviousType, -1 );
            }
            else
            {
                Throw.DebugAssert( fields.All( f => f.IsUnnamed && f.Type.IsOblivious ) );
                Throw.DebugAssert( "The oblivious is unnamed.", regularType == null );
                // For value type, oblivious is the non nullable.
                _obliviousType = this;
            }
            _fields = fields;
            _isReadOnlyCompliant = isReadOnlyCompliant;

            Throw.DebugAssert( "The regular has no field names and all its field types are regular.",
                               regularType == null || regularType.Fields.All( f => f.IsUnnamed && f.Type.IsRegular ) );
            Throw.DebugAssert( "If we are the regular, we have no field names and all our field types are regular.",
                               regularType != null || _fields.All( f => f.IsUnnamed && f.Type.IsRegular ) );
            _regularType = regularType ?? this;

            foreach( var f in fields ) f.SetOwner( this );
            _defInfo = CompositeHelper.CreateDefaultValueInfo( s.StringBuilderPool, this );

        }

        public override DefaultValueInfo DefaultValueInfo => _defInfo;

        new Null Nullable => Unsafe.As<Null>( base.Nullable );

        public ExternalNameAttribute? ExternalName => null;

        public string ExternalOrCSharpName => CSharpName;

        public override bool IsReadOnlyCompliant => _isReadOnlyCompliant;

        public override IRecordPocoType ObliviousType => _obliviousType;
        // Required... C# "Covariant return type" can do better...
        ICompositePocoType ICompositePocoType.ObliviousType => _obliviousType;

        public override IRecordPocoType RegularType => _regularType;

        public IReadOnlyList<IRecordPocoField> Fields => _fields;
        IReadOnlyList<IBasePocoField> IBaseCompositeType.Fields => _fields;

        IReadOnlyList<IPocoField> ICompositePocoType.Fields => _fields;

        public bool IsAnonymous => true;

        IRecordPocoType IRecordPocoType.Nullable => Nullable;
        IRecordPocoType IRecordPocoType.NonNullable => this;

        ICompositePocoType ICompositePocoType.Nullable => Nullable;
        ICompositePocoType ICompositePocoType.NonNullable => this;

        INamedPocoType INamedPocoType.Nullable => Nullable;
        INamedPocoType INamedPocoType.NonNullable => this;

        public override bool IsSubTypeOf( IPocoType type )
        {
            // type.IsNullable may be true: we don't care.
            if( type.NonNullable == this || type.Kind == PocoTypeKind.Any ) return true;
            if( type.Kind != PocoTypeKind.AnonymousRecord ) return false;
            var aType = (RecordAnonType)type.NonNullable;
            if( _fields.Length != aType._fields.Length ) return false;
            for( int i = 0; i < _fields.Length; i++ )
            {
                if( !_fields[i].Type.IsSubTypeOf( aType._fields[i].Type ) ) return false;
            }
            return true;
        }
    }

}
