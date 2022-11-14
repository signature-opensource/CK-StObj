using CK.CodeGen;
using CK.Core;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal sealed class KeyUnionTypes : IEquatable<KeyUnionTypes>
        {
            readonly IPocoType[] _types;
            readonly int _hash;

            public KeyUnionTypes( IPocoType[] types )
            {
                _types = types;
                var h = new HashCode();
                foreach( var t in types ) h.Add( t );
                _hash = h.ToHashCode();
            }

            public bool Equals( KeyUnionTypes? other ) => other != null ? _types.SequenceEqual( other._types ) : false;

            public override bool Equals( object? obj ) => Equals( obj as KeyUnionTypes );

            public override int GetHashCode() => _hash;
        }

        internal static UnionType CreateUnion( IActivityMonitor monitor,
                                               PocoTypeSystem s,
                                               IPocoType[] allowedTypes )
        {
            return new UnionType( s, allowedTypes );
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

                IOneOfPocoType<IPocoType> IOneOfPocoType<IPocoType>.NonNullable => NonNullable;
                IUnionPocoType IUnionPocoType.NonNullable => NonNullable;

                IOneOfPocoType<IPocoType> IOneOfPocoType<IPocoType>.Nullable => this;
                IUnionPocoType IUnionPocoType.Nullable => this;
            }
            IReadOnlyList<IPocoType> _allowedTypes;
            DefaultValueInfo _defInfo;

            public UnionType( PocoTypeSystem s, IPocoType[] allowedTypes )
                : base( s,
                        typeof(object),
                        "object",
                        PocoTypeKind.UnionType,
                        t => new Null( t ) )
            {
                _allowedTypes = allowedTypes;
                // Finds the first type that has a non-disallowed default.
                _defInfo = _allowedTypes.Select( t => t.DefaultValueInfo ).FirstOrDefault( d => !d.IsDisallowed );
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            public override bool IsAbstract => _allowedTypes.All( t => t.IsAbstract );


            IReadOnlyList<IPocoType> AllowedTypes => _allowedTypes;

            IEnumerable<IPocoType> IOneOfPocoType<IPocoType>.AllowedTypes => _allowedTypes;

            IOneOfPocoType<IPocoType> IOneOfPocoType<IPocoType>.Nullable => Nullable;
            IUnionPocoType IUnionPocoType.Nullable => Nullable;

            IOneOfPocoType<IPocoType> IOneOfPocoType<IPocoType>.NonNullable => this;
            IUnionPocoType IUnionPocoType.NonNullable => this;


        }
    }

}
