using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using static CK.CodeGen.TupleTypeName;
using System.Threading;
using System.Text;
using System.Collections.Immutable;

namespace CK.Setup
{
    partial class PocoType
    {
        internal sealed class KeyUnionTypes : IEquatable<KeyUnionTypes>
        {
            public readonly ImmutableArray<IPocoType> Types;
            readonly int _hash;

            public KeyUnionTypes( ImmutableArray<IPocoType> types, out bool isOblivious )
            {
                isOblivious = true;
                Types = types;
                var h = new HashCode();
                foreach( var t in types )
                {
                    isOblivious &= t.IsOblivious;
                    h.Add( t );
                }
                _hash = h.ToHashCode();
            }

            public bool Equals( KeyUnionTypes? other ) => other != null && Types.SequenceEqual( other.Types );

            public override bool Equals( object? obj ) => Equals( obj as KeyUnionTypes );

            public override int GetHashCode() => _hash;
        }

        internal static UnionType CreateUnion( IActivityMonitor monitor,
                                               PocoTypeSystemBuilder s,
                                               KeyUnionTypes key,
                                               IPocoType? obliviousType )
        {
            return new UnionType( monitor, s, key, (IUnionPocoType?)obliviousType );
        }

        internal sealed class UnionType : PocoType, IUnionPocoType
        {
            sealed class Null : NullReferenceType, IUnionPocoType
            {
                public Null( IPocoType notNullable )
                    : base( notNullable )
                {
                }

                new UnionType NonNullable => Unsafe.As<UnionType>( base.NonNullable );

                public IEnumerable<IPocoType> AllowedTypes => NonNullable.AllowedTypes.Concat( NonNullable.AllowedTypes.Select( a => a.Nullable ) );

                IOneOfPocoType IOneOfPocoType.NonNullable => NonNullable;
                IUnionPocoType IUnionPocoType.NonNullable => NonNullable;

                IOneOfPocoType IOneOfPocoType.Nullable => this;
                IUnionPocoType IUnionPocoType.Nullable => this;
            }

            readonly KeyUnionTypes _k;
            readonly IUnionPocoType _obliviousType;
            string? _standardName;

            public UnionType( IActivityMonitor monitor, PocoTypeSystemBuilder s, KeyUnionTypes k, IUnionPocoType? obliviousType )
                : base( s,
                        typeof( object ),
                        "object",
                        PocoTypeKind.UnionType,
                        static t => new Null( t ) )
            {
                _k = k;
                if( obliviousType != null )
                {
                    Throw.DebugAssert( obliviousType.IsOblivious && obliviousType.AllowedTypes.All( t => t.IsOblivious ) );
                    _obliviousType = obliviousType;
                    // Registers the back reference to the oblivious type.
                    _ = new PocoTypeRef( this, obliviousType, -1 );
                }
                else
                {
                    Throw.DebugAssert( k.Types.All( t => t.IsOblivious ) );
                    _obliviousType = this;
                }
                int i = 0;
                foreach( var t in _k.Types )
                {
                    _ = new PocoTypeRef( this, t, i++ );
                }
            }

            public override bool CanReadFrom( IPocoType type )
            {
                if( type == this || type.Kind == PocoTypeKind.Any ) return true;
                // To allow the type to be readable, it must be readable.
                return type.IsNullable && _k.Types.Any( a => a.CanReadFrom( type ) );
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public override string StandardName
            {
                get
                {
                    if( _standardName == null )
                    {
                        var b = new StringBuilder();
                        foreach( var a in AllowedTypes )
                        {
                            if( b.Length > 0 ) b.Append( '|' );
                            b.Append( a.StandardName );
                        }
                        _standardName = b.ToString();
                    }
                    return _standardName;
                }
            }

            public override IPocoType ObliviousType => _obliviousType;

            IReadOnlyList<IPocoType> AllowedTypes => _k.Types;

            IEnumerable<IPocoType> IOneOfPocoType.AllowedTypes => _k.Types;

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;
            IUnionPocoType IUnionPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
            IUnionPocoType IUnionPocoType.NonNullable => this;


        }
    }

}
