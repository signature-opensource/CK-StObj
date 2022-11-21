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

            public bool Equals( KeyUnionTypes? other ) => other != null && _types.SequenceEqual( other._types );

            public override bool Equals( object? obj ) => Equals( obj as KeyUnionTypes );

            public override int GetHashCode() => _hash;
        }

        internal static UnionType CreateUnion( IActivityMonitor monitor,
                                               PocoTypeSystem s,
                                               IPocoType[] allowedTypes )
        {
            return new UnionType( monitor, s, allowedTypes );
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

            readonly IReadOnlyList<IPocoType> _allowedTypes;
            readonly DefaultValueInfo _defInfo;

            public UnionType( IActivityMonitor monitor, PocoTypeSystem s, IPocoType[] allowedTypes )
                : base( s,
                        typeof(object),
                        "object",
                        PocoTypeKind.UnionType,
                        t => new Null( t ) )
            {
                _allowedTypes = allowedTypes;
                // Finds the first type that has a non-disallowed default.
                _defInfo = _allowedTypes.Select( t => t.DefaultValueInfo ).FirstOrDefault( d => !d.IsDisallowed );
                // Sets the initial IsExchangeable status.
                bool initialIsExchangeable = false;
                for( int i = 0; i < allowedTypes.Length; i++ )
                {
                    var t = allowedTypes[i];
                    _ = new PocoTypeRef( this, t, i );
                    initialIsExchangeable |= t.IsExchangeable;
                }
                // Sets the initial IsExchangeable status.
                if( !initialIsExchangeable )
                {
                    SetNotExchangeable( monitor, "none of its types are exchangeable." );
                }
            }

            protected override void OnNoMoreExchangeable( IActivityMonitor monitor, IPocoType.ITypeRef r )
            {
                if( IsExchangeable && !_allowedTypes.Any( t => t.IsExchangeable ) )
                {
                    SetNotExchangeable( monitor, $"its last type '{r.Type}' is not exchangeable." );
                }
            }

            new Null Nullable => Unsafe.As<Null>( base.Nullable );

            public override DefaultValueInfo DefaultValueInfo => _defInfo;

            public override bool IsAbstract => _allowedTypes.All( t => t.IsAbstract );


            IReadOnlyList<IPocoType> AllowedTypes => _allowedTypes;

            IEnumerable<IPocoType> IOneOfPocoType.AllowedTypes => _allowedTypes;

            IOneOfPocoType IOneOfPocoType.Nullable => Nullable;
            IUnionPocoType IUnionPocoType.Nullable => Nullable;

            IOneOfPocoType IOneOfPocoType.NonNullable => this;
            IUnionPocoType IUnionPocoType.NonNullable => this;


        }
    }

}
