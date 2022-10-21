using CK.CodeGen;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    partial class PocoType
    {
        internal static CollectionType1 CreateCollection( PocoTypeSystem s,
                                                          Type tCollection,
                                                          string typeName,
                                                          PocoTypeKind kind,
                                                          IPocoType itemType )
        {
            return new CollectionType1( s, tCollection, typeName, kind, itemType );
        }

        internal static CollectionType2 CreateCollection( PocoTypeSystem s,
                                                          Type tCollection,
                                                          string typeName,
                                                          PocoTypeKind kind,
                                                          IPocoType itemType1,
                                                          IPocoType itemType2 )
        {
            return new CollectionType2( s, tCollection, typeName, kind, itemType1, itemType2 );
        }

        sealed class NullCollection : NullReferenceType, ICollectionPocoType
        {
            public NullCollection( IPocoType notNullable )
                : base( notNullable )
            {
            }

            new ICollectionPocoType NonNullable => Unsafe.As<ICollectionPocoType>( base.NonNullable );

            public IReadOnlyList<IPocoType> ItemTypes => NonNullable.ItemTypes;

            ICollectionPocoType ICollectionPocoType.NonNullable => NonNullable;

            ICollectionPocoType ICollectionPocoType.Nullable => this;
        }

        // List, HashSet, Array.
        internal sealed class CollectionType1 : PocoType, ICollectionPocoType, IReadOnlyList<IPocoType>
        {
            readonly IPocoType _itemType;
            readonly IPocoFieldDefaultValue _def;

            readonly static MethodInfo _arrayEmpty = typeof( Array ).GetMethod( "Empty" )!;

            public CollectionType1( PocoTypeSystem s,
                                    Type tCollection,
                                    string typeName,
                                    PocoTypeKind kind,
                                    IPocoType itemType )
                : base( s, tCollection, typeName, kind, t => new NullCollection( t ) )
            {
                _itemType = itemType;
                _def = tCollection.IsArray
                        ? new FieldDefaultValue( _arrayEmpty.MakeGenericMethod( itemType.Type ).Invoke( null, Type.EmptyTypes )!, $"System.Array.Empty<{itemType.CSharpName}>()" )
                        : new FieldDefaultValue( Activator.CreateInstance( tCollection )!, $"new {CSharpName}()" );
            }

            new NullCollection Nullable => Unsafe.As<NullCollection>( base.Nullable );

            public IReadOnlyList<IPocoType> ItemTypes => this;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;

            int IReadOnlyCollection<IPocoType>.Count => 1;

            IPocoType IReadOnlyList<IPocoType>.this[int index]
            {
                get
                {
                    Throw.CheckOutOfRangeArgument( index == 0 );
                    return _itemType;
                }
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            IEnumerator<IPocoType> IEnumerable<IPocoType>.GetEnumerator() => new CKEnumeratorMono<IPocoType>( _itemType );

            IEnumerator IEnumerable.GetEnumerator() => new CKEnumeratorMono<IPocoType>( _itemType );
        }

        // Dictionary.
        internal sealed class CollectionType2 : PocoType, ICollectionPocoType
        {
            readonly IPocoType[] _itemTypes;
            readonly IPocoFieldDefaultValue _def;

            public CollectionType2( PocoTypeSystem s,
                                    Type tCollection,
                                    string typeName,
                                    PocoTypeKind kind,
                                    IPocoType itemType1,
                                    IPocoType itemType2 )
                : base( s, tCollection, typeName, kind, t => new NullCollection( t ) )
            {
                _itemTypes = new[] { itemType1, itemType2 };
                _def = new FieldDefaultValue( Activator.CreateInstance( tCollection )!, $"new {CSharpName}()" );
            }

            public override DefaultValueInfo DefaultValueInfo => new DefaultValueInfo( _def );

            new NullCollection Nullable => Unsafe.As<NullCollection>( base.Nullable );

            public IReadOnlyList<IPocoType> ItemTypes => _itemTypes;

            ICollectionPocoType ICollectionPocoType.Nullable => Nullable;

            ICollectionPocoType ICollectionPocoType.NonNullable => this;
        }

    }
}
