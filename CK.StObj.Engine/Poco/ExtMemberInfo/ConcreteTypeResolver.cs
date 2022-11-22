using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates simple type inference that resolves multiple root type declarations
    /// to a single most concrete, writable and non nullable type.
    /// </summary>
    public static class ConcreteTypeResolver
    {
        /// <summary>
        /// Captures an inferred type that is the most concrete and writable as possible.
        /// When it is combined with other results, it becomes also as "non nullable" as possible.
        /// </summary>
        public readonly struct Result
        {
            internal Result( IExtNullabilityInfo resolved, TupleElementNamesAttribute? tupleNames, bool isWritableCollection )
            {
                Resolved = resolved;
                TupleNames = tupleNames;
                IsWritableCollection = isWritableCollection;
            }

            /// <summary>
            /// Gets the initial and inferred types.
            /// </summary>
            public IExtNullabilityInfo Resolved { get; }

            /// <summary>
            /// Gets the tuple names if it exists.
            /// </summary>
            public TupleElementNamesAttribute? TupleNames { get; }

            /// <summary>
            /// Gets whether the initial type was a writable collection (but not an array):
            /// a List, Set or Dictionary but not a IReadOnly collection.
            /// <para>
            /// A type must be fully mutable or fully readonly. However records are allowed
            /// since as value types, they are copied and are harmless when projected in a
            /// read only abstract collection.
            /// </para>
            /// </summary>
            public bool IsWritableCollection { get; }

            internal Result? CombineWith( IActivityMonitor monitor, IPocoTypeSystem typeSystem, in Result other )
            {
                return Combine( monitor, typeSystem, this, other );

                static Result? Combine( IActivityMonitor monitor, IPocoTypeSystem typeSystem, in Result left, in Result right )
                {
                    var t = Combine( monitor, typeSystem, left.Resolved, right.Resolved );
                    if( t == null ) return null;
                    var tupleNames = left.TupleNames;
                    if( tupleNames == null || (right.TupleNames != null && right.TupleNames.TransformNames.Count > tupleNames.TransformNames.Count) )
                    {
                        tupleNames = right.TupleNames;
                    }
                    return new Result( t, tupleNames, left.IsWritableCollection || right.IsWritableCollection );

                    /// <summary>
                    /// Combines two types into a more "specialized" one: non nullable, array and IList/ISet/IDictionary&lt;&gt;
                    /// over concrete List/Set/Dictionary&lt;&gt; wins.
                    /// </summary>
                    static IExtNullabilityInfo? Combine( IActivityMonitor monitor,
                                                         IPocoTypeSystem typeSystem,
                                                         IExtNullabilityInfo left,
                                                         IExtNullabilityInfo right )
                    {
                        // Handling nullability once for all: projects both to non nullable
                        // as soon as one is non nullable.
                        if( !left.IsNullable || !right.IsNullable )
                        {
                            left = left.ToNonNullable();
                            right = right.ToNonNullable();
                        }
                        Debug.Assert( left.IsNullable == right.IsNullable );
                        // Handling object on both sides: the non object wins.
                        if( left.Type == typeof( object ) ) return right;
                        if( right.Type == typeof( object ) ) return left;
                        // Handling value type on both sides: the types must exactly match.
                        if( right.Type.IsValueType || left.Type.IsValueType )
                        {
                            if( right.Type == left.Type ) return right;
                            return ReconciliationError( monitor, left, right );
                        }
                        // String is the only basic type that is not a value type.
                        if( left.Type == typeof( string ) || right.Type == typeof( string ) )
                        {
                            if( right.Type == left.Type ) return right;
                            return ReconciliationError( monitor, left, right );
                        }
                        // Handling array on both sides.
                        if( left.Type.IsSZArray ) return OnLeftArray( monitor, left, right );
                        if( right.Type.IsSZArray ) return OnLeftArray( monitor, right, left );
                        // Handling generic that must be collections.
                        if( left.GenericTypeArguments.Count > 0 || right.GenericTypeArguments.Count > 0 )
                        {
                            // List/Set vs. Dictionary mismatch.
                            if( left.GenericTypeArguments.Count != right.GenericTypeArguments.Count )
                            {
                                return ReconciliationError( monitor, left, right );
                            }
                            // We consider the IXXX (that leads to a potentially covariant implementation)
                            // better that XXX concrete .Net class. Whether this will work or not, is let
                            // to subsequent steps.
                            if( left.GenericTypeArguments.Count == 2 )
                            {
                                return OnDictionary( monitor, typeSystem, left, right );
                            }
                            return OnListOrSet( monitor, typeSystem, left, right );
                        }
                        // We are left with IPoco interfaces.
                        Debug.Assert( typeof( IPoco ).IsAssignableFrom( left.Type ) && typeof( IPoco ).IsAssignableFrom( right.Type ) );
                        // It's useless here to try do detect an incompatibility between the 2 interfaces.
                        // The only thing we must do is trying to select a family if possible so we don't
                        // erase any "concrete" Poco, letting a last "abstract" win the choice.
                        var fLeft = typeSystem.GetPrimaryPocoType( left.Type );
                        if( fLeft != null ) return left;
                        // Left is abstract: right may be a concrete one...
                        return right;

                        static IExtNullabilityInfo? OnLeftArray( IActivityMonitor monitor, IExtNullabilityInfo left, IExtNullabilityInfo right )
                        {
                            Debug.Assert( left.Type.IsSZArray && left.ElementType != null );
                            if( right.Type.IsSZArray )
                            {
                                Debug.Assert( right.ElementType != null );
                                if( right.ElementType!.Type == left.ElementType.Type && right.IsNullable == left.IsNullable ) return left;
                                return ReconciliationError( monitor, left, right );
                            }
                            // We are working on mapped types here.
                            // A IReadOnlyList<T> may have been used for the array and if it's the case,
                            // it has been mapped to IList<T>.
                            // The Array always wins, so allowing a IList<T> (whatever T is) let the final type
                            // checking do the job.
                            if( right.GenericTypeArguments.Count == 1
                                && right.Type.GetGenericTypeDefinition() == typeof( IList<> ) )
                            {
                                return left;
                            }
                            return ReconciliationError( monitor, left, right );
                        }

                        static IExtNullabilityInfo? OnListOrSet( IActivityMonitor monitor, IPocoTypeSystem typeSystem, IExtNullabilityInfo left, IExtNullabilityInfo right )
                        {
                            // Kind must be the same (List vs. Set).
                            bool leftIsCov = left.Type.IsInterface;
                            bool rightIsCov = right.Type.IsInterface;
                            var tGenLeft = left.Type.GetGenericTypeDefinition();
                            var tGenRight = left.Type.GetGenericTypeDefinition();
                            Debug.Assert( tGenLeft == typeof( IList<> ) || tGenLeft == typeof( List<> ) || tGenLeft == typeof( ISet<> ) || tGenLeft == typeof( HashSet<> ) );
                            Debug.Assert( tGenRight == typeof( IList<> ) || tGenRight == typeof( List<> ) || tGenRight == typeof( ISet<> ) || tGenRight == typeof( HashSet<> ) );

                            bool isLeftList = leftIsCov ? tGenLeft == typeof( IList<> ) : tGenLeft == typeof( List<> );
                            bool isRightList = rightIsCov ? tGenRight == typeof( IList<> ) : tGenRight == typeof( List<> );
                            if( isLeftList != isRightList )
                            {
                                return ReconciliationError( monitor, left, right );
                            }
                            var item = Combine( monitor, typeSystem, left.GenericTypeArguments[0], right.GenericTypeArguments[0] );
                            if( item == null ) return null;
                            if( item == left.GenericTypeArguments[0] && (leftIsCov || leftIsCov == rightIsCov) ) return left;
                            if( item == right.GenericTypeArguments[0] && (rightIsCov || leftIsCov == rightIsCov) ) return right;
                            Type tGen = leftIsCov || rightIsCov
                                            ? (isLeftList ? typeof( IList<> ) : typeof( ISet<> ))
                                            : (isLeftList ? typeof( List<> ) : typeof( HashSet<> ));
                            return new ExtNullabilityInfo( tGen.MakeGenericType( item.Type ), item, left.IsNullable );
                        }

                        static IExtNullabilityInfo? ReconciliationError( IActivityMonitor monitor, IExtNullabilityInfo left, IExtNullabilityInfo right )
                        {
                            monitor.Error( $"Unable to conciliate '{left.Type:C}' and '{right.Type:C}' types." );
                            return null;
                        }

                        static IExtNullabilityInfo? OnDictionary( IActivityMonitor monitor, IPocoTypeSystem typeSystem, IExtNullabilityInfo left, IExtNullabilityInfo right )
                        {
                            Debug.Assert( left.Type.GetGenericTypeDefinition() == typeof( IDictionary<,> ) || left.Type.GetGenericTypeDefinition() == typeof( Dictionary<,> ) );
                            Debug.Assert( right.Type.GetGenericTypeDefinition() == typeof( IDictionary<,> ) || right.Type.GetGenericTypeDefinition() == typeof( Dictionary<,> ) );
                            // Key is invariant and non nullable by design.
                            var k = left.GenericTypeArguments[0];
                            if( k.Type != right.GenericTypeArguments[0].Type )
                            {
                                return ReconciliationError( monitor, left, right );
                            }
                            bool leftIsCov = left.Type.IsInterface;
                            bool rightIsCov = right.Type.IsInterface;
                            var v = Combine( monitor, typeSystem, left.GenericTypeArguments[1], right.GenericTypeArguments[1] );
                            if( v == null ) return null;
                            if( v == left.GenericTypeArguments[1] && (leftIsCov || leftIsCov == rightIsCov) ) return left;
                            if( v == right.GenericTypeArguments[1] && (rightIsCov || leftIsCov == rightIsCov) ) return right;
                            Type tGen = leftIsCov || rightIsCov ? typeof( IDictionary<,> ) : typeof( Dictionary<,> ); ;
                            return new ExtNullabilityInfo( tGen.MakeGenericType( k.Type, v.Type ), k, v, left.IsNullable );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Resolves one or more root types to a single most concrete, writable and non nullable type.
        /// </summary>
        /// <param name="monitor">The monitor that will receive errors.</param>
        /// <param name="typeSystem">The type system.</param>
        /// <param name="types">The root types to process.</param>
        /// <returns>A final result or null on error.</returns>
        public static Result? ToConcrete( IActivityMonitor monitor,
                                          IPocoTypeSystem typeSystem,
                                          IEnumerable<IExtMemberInfo> types )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( typeSystem );
            Throw.CheckNotNullArgument( types );
            var e = types.GetEnumerator();
            Throw.CheckArgument( e.MoveNext() );
            var left = ToConcrete( monitor, typeSystem, e.Current );
            if( left == null ) return null;

            while( e.MoveNext() )
            {
                var right = ToConcrete( monitor, typeSystem, e.Current );
                if( right == null ) return null;
                left = left.Value.CombineWith( monitor, typeSystem, right.Value );
                if( left == null ) return null;
            }
            return left;
        }

        struct RooContext
        {
            public readonly IExtMemberInfo Root;

            public readonly IPocoTypeSystem TypeSystem;

            public bool? ReadOnly { get; private set; }

            public PocoTypeKind RootKind { get; private set; }

            public RooContext( IPocoTypeSystem typeSystem, IExtMemberInfo root )
            {
                Root = root;
                TypeSystem = typeSystem;
                ReadOnly = null;
                RootKind = PocoTypeKind.None;
            }

            public bool SetArrayContext( IActivityMonitor monitor, Type array )
            {
                Debug.Assert( array.IsSZArray );
                if( RootKind == PocoTypeKind.None ) RootKind = PocoTypeKind.Array;
                return SetReadOnlyContext( monitor, true, array );
            }

            public bool SetCollectionContext( IActivityMonitor monitor, bool readOnly, Type collection, PocoTypeKind kind )
            {
                if( RootKind == PocoTypeKind.None ) RootKind = kind;
                return SetReadOnlyContext( monitor, readOnly, collection );
            }

            public bool SetReadOnlyContext( IActivityMonitor monitor, bool readOnly, Type subType )
            {
                if( ReadOnly.HasValue )
                {
                    if( ReadOnly.Value != readOnly )
                    {
                        if( ReadOnly.Value )
                        {
                            monitor.Error( $"Invalid {Root}: readonly type '{Root.Type:C}' cannot contain a mutable type '{subType:C}'." );
                        }
                        else
                        {
                            monitor.Error( $"Invalid {Root}: mutable type '{Root.Type:C}' cannot contain a readonly type '{subType:C}'." );
                        }
                        return false;
                    }
                    return true;
                }
                ReadOnly = readOnly;
                return true;
            }
        }

        internal static Result? ToConcrete( IActivityMonitor monitor,
                                            IPocoTypeSystem typeSystem,
                                            IExtMemberInfo root )
        {
            var t = root.GetHomogeneousNullabilityInfo( monitor );
            if( t == null ) return null;
            var ctx = new RooContext( typeSystem, root );
            var tR = ToConcrete( monitor, t, ref ctx );
            if( tR == null ) return null;
            var tupleNames = root.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault();
            return new Result( tR, tupleNames, ctx.ReadOnly == false && ctx.RootKind != PocoTypeKind.Array );

            static IExtNullabilityInfo? ToConcrete( IActivityMonitor monitor, IExtNullabilityInfo t, ref RooContext ctx )
            {
                var type = t.Type;
                if( type == typeof( object ) || type == typeof( string ) ) return t;
                // No ReadOnly<T1,T2...> for ValueTuple yet.
                if( type.IsValueType ) return t;
                if( type.IsSZArray ) return OnArray( monitor, t, ref ctx );
                if( t.GenericTypeArguments.Count > 0 )
                {
                    if( t.GenericTypeArguments.Count > 2 ) return UnsupportedTypeError( monitor, t.Type );
                    return OnGeneric( monitor, t, ref ctx );
                }
                // We are left with IPoco interfaces.
                if( !typeof( IPoco ).IsAssignableFrom( t.Type ) ) return UnsupportedTypeError( monitor, t.Type );
                // If the poco is not abstract, resolves its primary type.
                var concrete = ctx.TypeSystem.GetPrimaryPocoType( t.Type );
                return concrete != null
                        ? new ExtNullabilityInfo( concrete.Type, t.IsNullable )
                        : t;

                static IExtNullabilityInfo? OnArray( IActivityMonitor monitor, IExtNullabilityInfo t, ref RooContext ctx )
                {
                    Debug.Assert( t.ElementType != null );
                    if( !ctx.SetArrayContext( monitor, t.Type ) ) return null;
                    var tE = ToConcrete( monitor, t.ElementType, ref ctx );
                    if( tE == null ) return null;
                    return tE == t.ElementType
                            ? t
                            : new ExtNullabilityInfo( tE.Type.MakeArrayType(), tE, t.IsNullable );
                }

                static IExtNullabilityInfo? OnGeneric( IActivityMonitor monitor, IExtNullabilityInfo t, ref RooContext ctx )
                {
                    var tGen = t.Type.GetGenericTypeDefinition();
                    Type? tGenMapped = MapGeneric( monitor, tGen, ref ctx );
                    if( tGenMapped == null ) return null;

                    var t0 = ToConcrete( monitor, t.GenericTypeArguments[0], ref ctx );
                    if( t0 == null ) return null;
                    if( t.GenericTypeArguments.Count == 1 )
                    {
                        return tGen == tGenMapped && t0 == t.GenericTypeArguments[0]
                                ? t
                                : new ExtNullabilityInfo( tGenMapped.MakeGenericType( t0.Type ), t0, t.IsNullable );
                    }
                    var t1 = ToConcrete( monitor, t.GenericTypeArguments[1], ref ctx );
                    if( t1 == null ) return null;
                    return tGen == tGenMapped && t0 == t.GenericTypeArguments[0] && t1 == t.GenericTypeArguments[1]
                            ? t
                            : new ExtNullabilityInfo( tGenMapped.MakeGenericType( t0.Type, t1.Type ), t0, t1, t.IsNullable );

                    static Type? MapGeneric( IActivityMonitor monitor, Type tGen, ref RooContext ctx )
                    {
                        if( tGen == typeof( IReadOnlyList<> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, true, tGen, PocoTypeKind.List ) ) return null;
                            return typeof( IList<> );
                        }
                        if( tGen == typeof( IList<> ) || tGen == typeof( List<> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, false, tGen, PocoTypeKind.List ) ) return null;
                            return tGen;
                        }
                        if( tGen == typeof( IReadOnlySet<> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, true, tGen, PocoTypeKind.HashSet ) ) return null;
                            return typeof( ISet<> );
                        }
                        if( tGen == typeof( ISet<> ) || tGen == typeof( HashSet<> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, false, tGen, PocoTypeKind.HashSet ) ) return null;
                            return tGen;
                        }
                        if( tGen == typeof( IReadOnlyDictionary<,> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, true, tGen, PocoTypeKind.Dictionary ) ) return null;
                            return typeof( IDictionary<,> );
                        }
                        if( tGen == typeof( IDictionary<,> ) || tGen == typeof( Dictionary<,> ) )
                        {
                            if( !ctx.SetCollectionContext( monitor, false, tGen, PocoTypeKind.Dictionary ) ) return null;
                            return tGen;
                        }
                        return UnsupportedTypeError( monitor, tGen )?.Type;
                    }
                }

                static IExtNullabilityInfo? UnsupportedTypeError( IActivityMonitor monitor, Type type )
                {
                    monitor.Error( $"Not supported type: {type:C}." );
                    return null;
                }
            }

        }

    }
}
