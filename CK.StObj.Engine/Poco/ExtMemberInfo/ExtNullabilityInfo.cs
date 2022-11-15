using CK.Core;
using CK.Core.Impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Setup
{
    sealed class ExtNullabilityInfo : IExtNullabilityInfo
    {
        readonly Type _type;
        readonly object _subTypes;
        readonly bool _isNullable;
        readonly bool _useReadState;
        readonly bool _homogeneous;

        internal ExtNullabilityInfo( TEMPNullabilityInfo nInfo, bool useReadState, bool singleState )
        {
            if( (_type = nInfo.Type).IsByRef ) _type = _type.GetElementType()!;
            _useReadState = useReadState;
            _isNullable = useReadState ? nInfo.ReadState != NullabilityState.NotNull : nInfo.WriteState != NullabilityState.NotNull;
            _homogeneous = singleState || nInfo.ReadState == nInfo.WriteState;
            if( nInfo.ElementType != null )
            {
                var s = new ExtNullabilityInfo( nInfo.ElementType, useReadState, true );
                _subTypes = s;
            }
            else if( nInfo.GenericTypeArguments.Length > 0 )
            {
                var a = new IExtNullabilityInfo[nInfo.GenericTypeArguments.Length];
                for( int i = 0; i < nInfo.GenericTypeArguments.Length; ++i )
                {
                    var s = new ExtNullabilityInfo( nInfo.GenericTypeArguments[i], useReadState, true );
                    a[i] = s;
                }
                _subTypes = a;
            }
            else
            {
                _subTypes = Array.Empty<ExtNullabilityInfo>();
            }
        }

        internal ExtNullabilityInfo( Type type, bool isNullable )
        {
            Throw.CheckArgument( !type.IsGenericType && !type.IsArray );
            _type = type;
            _subTypes = Array.Empty<IReadOnlyList<IExtNullabilityInfo>>();
            _isNullable = isNullable;
            _useReadState = true;
            _homogeneous = true;
        }

        ExtNullabilityInfo( Type t, object subTypes, bool isNullable, bool useReadState, bool homogeneous )
        {
            Debug.Assert( t != null );
            Debug.Assert( !t.IsValueType || isNullable == (Nullable.GetUnderlyingType( t ) != null) );
            _type = t;
            _subTypes = subTypes;
            _isNullable = isNullable;
            _useReadState = useReadState;
            _homogeneous = homogeneous;
        }

        ExtNullabilityInfo( Type t, IExtNullabilityInfo a, bool isNullable )
        {
            Debug.Assert( !t.IsValueType && t.IsGenericType && t.GetGenericTypeDefinition().GenericTypeArguments.Length == 1 );
            _type = t;
            _subTypes = a;
            _isNullable = isNullable;
            _useReadState = true;
            _homogeneous = true;
        }

        ExtNullabilityInfo( Type t, IExtNullabilityInfo a0, IExtNullabilityInfo a1, bool isNullable )
        {
            Debug.Assert( !t.IsValueType && t.IsGenericType && t.GetGenericTypeDefinition().GenericTypeArguments.Length == 2 );
            _type = t;
            _subTypes = new[] { a0, a1 };
            _isNullable = isNullable;
            _useReadState = true;
            _homogeneous = true;
        }

        public Type Type => _type;

        public bool IsNullable => _isNullable;

        public bool ReflectsReadState => _useReadState || _homogeneous;

        public bool ReflectsWriteState => !_useReadState || _homogeneous;

        public bool IsHomogeneous => _homogeneous;

        public IExtNullabilityInfo ToNonNullable()
        {
            if( !_isNullable ) return this;
            var t = _type;
            if( t.IsValueType )
            {
                t = Nullable.GetUnderlyingType( t );
                Debug.Assert( t != null );
            }
            return new ExtNullabilityInfo( t, _subTypes, false, _useReadState, _homogeneous );
        }

        public IExtNullabilityInfo ToNullable()
        {
            if( _isNullable ) return this;
            var t = _type;
            if( t.IsValueType )
            {
                t = typeof( Nullable<> ).MakeGenericType( _type );
            }
            return new ExtNullabilityInfo( t, _subTypes, true, _useReadState, _homogeneous );
        }

        public IExtNullabilityInfo? ElementType => _subTypes as ExtNullabilityInfo;

        public IReadOnlyList<IExtNullabilityInfo> GenericTypeArguments => _subTypes as IReadOnlyList<IExtNullabilityInfo> ?? Array.Empty<ExtNullabilityInfo>();

        public StringBuilder ToString( StringBuilder b )
        {
            if( _subTypes is ExtNullabilityInfo i )
            {
                i.ToString( b ).Append( "[]" );
            }
            else
            {
                b.Append( _type.Name );
                if( _subTypes is IExtNullabilityInfo[] a && a.Length > 0 )
                {
                    b.Append( '<' );
                    bool atLeastOne = false;
                    foreach( var s in a )
                    {
                        if( atLeastOne ) b.Append( ',' );
                        else atLeastOne = true;
                        ((ExtNullabilityInfo)s).ToString( b );
                    }
                    b.Append( '>' );
                }
            }
            return _isNullable ? b.Append( '?' ) : b;
        }

        public override string ToString() => ToString( new StringBuilder() ).ToString(); 

        public static IExtNullabilityInfo? ToConcrete( IActivityMonitor monitor,
                                                       IPocoTypeSystem typeSystem,
                                                       IEnumerable<IExtMemberInfo> types,
                                                       out TupleElementNamesAttribute? longestTupleNames )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( typeSystem );
            Throw.CheckNotNullArgument( types );
            var e = types.GetEnumerator();
            Throw.CheckArgument( e.MoveNext() );

            longestTupleNames = null;
            var t = e.Current.GetHomogeneousNullabilityInfo( monitor );
            if( t == null ) return null;
            longestTupleNames = e.Current.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault();
            while( e.MoveNext() )
            {
                var h = e.Current.GetHomogeneousNullabilityInfo( monitor );
                if( h == null ) return null;
                var names = e.Current.GetCustomAttributes<TupleElementNamesAttribute>().FirstOrDefault();
                if( names != null
                    && (longestTupleNames == null || longestTupleNames.TransformNames.Count < names.TransformNames.Count) )
                {
                    longestTupleNames = names;
                }
                var c = CombineToConcrete( monitor, typeSystem, t, h );
                if( c == null ) return null;
                t = c;
            }
            return t;
        }

        /// <summary>
        /// Combines two info into a more specialized one: non nullable and most precise type wins.
        /// </summary>
        static IExtNullabilityInfo? CombineToConcrete( IActivityMonitor monitor,
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
            if( left.Type.IsSZArray ) return ReduceArray( monitor, left, right );
            if( right.Type.IsSZArray ) return ReduceArray( monitor, right, left );
            // Handling generic that must be collections.
            if( left.GenericTypeArguments.Count > 0 || right.GenericTypeArguments.Count > 0 )
            {
                if( left.GenericTypeArguments.Count > 2 ) return UnsupportedTypeError( monitor, left.Type );
                if( right.GenericTypeArguments.Count > 2 ) return UnsupportedTypeError( monitor, right.Type );
                if( left.GenericTypeArguments.Count != right.GenericTypeArguments.Count )
                {
                    return ReconciliationError( monitor, left, right );
                }
                // Generic type mappings: IReadOnlyXXX are mapped to their IXXX interface
                // so that out covariant types will be used.
                // Kind must be the same (List/Set/Dictionary).
                // Regular types are accepted. if a regular (List<>) appears on one side and
                // an abstraction (IList) on the other, we chose the abstraction to use our
                // covariant implementations. If they appear to eventually satisfy all the other
                // types (including the regular one), then it's cool.
                var tGenLeft = left.Type.GetGenericTypeDefinition();
                Type? tGenLeftM = MapGeneric( tGenLeft, out var leftKind, out var regularLeft );
                if( tGenLeftM == null ) return UnsupportedTypeError( monitor, left.Type );

                var tGenRight = right.Type.GetGenericTypeDefinition();
                Type? tGenRightM = MapGeneric( tGenRight, out var rightKind, out var regularRight );
                if( tGenRightM == null ) return UnsupportedTypeError( monitor, right.Type );

                if( rightKind != leftKind ) 
                {
                    return ReconciliationError( monitor, left, right );
                }
                bool leftIsGood = !regularLeft || regularRight;
                bool rightIsGood = !regularRight || regularLeft;

                if( rightKind == PocoTypeKind.Dictionary )
                {
                    // Key is invariant.
                    var k = left.GenericTypeArguments[0];
                    if( k.Type != right.GenericTypeArguments[0].Type )
                    {
                        return ReconciliationError( monitor, left, right );   
                    }
                    var v = CombineToConcrete( monitor, typeSystem, left.GenericTypeArguments[1], right.GenericTypeArguments[1] );
                    if( v == null ) return null;
                    if( leftIsGood && v == left.GenericTypeArguments[1] && tGenLeftM == tGenLeft ) return left;
                    if( rightIsGood && v == right.GenericTypeArguments[1] && tGenRightM == tGenRight ) return right;
                    var t2 = (leftIsGood ? tGenLeftM : tGenRightM).MakeGenericType( k.Type, v.Type );
                    return new ExtNullabilityInfo( t2, k, v, left.IsNullable );
                }
                var item = CombineToConcrete( monitor, typeSystem, left.GenericTypeArguments[0], right.GenericTypeArguments[0] );
                if( item == null ) return null;
                if( leftIsGood && item == left.GenericTypeArguments[0] && tGenLeftM == tGenLeft ) return left;
                if( rightIsGood && item == right.GenericTypeArguments[0] && tGenRightM == tGenRight ) return right;
                var t1 = (leftIsGood ? tGenLeftM : tGenRightM).MakeGenericType( item.Type );
                return new ExtNullabilityInfo( t1, item, left.IsNullable );
            }
            // We are left with IPoco interfaces.
            if( !typeof( IPoco ).IsAssignableFrom( left.Type ) ) return UnsupportedTypeError( monitor, left.Type );
            if( !typeof( IPoco ).IsAssignableFrom( right.Type ) ) return UnsupportedTypeError( monitor, right.Type );
            // It's useless here to try do detect an incompatibility between the 2 interfaces.
            // The only thing we must do is trying to select a family if possible so we don't
            // erase any "concrete" Poco, letting a last "abstract" win the choice.
            var fLeft = typeSystem.GetPrimaryPocoType( left.Type );
            if( fLeft != null ) return left;
            // Left is abstract: right may be a concrete one...
            return right;

            static IExtNullabilityInfo? ReduceArray( IActivityMonitor monitor, IExtNullabilityInfo left, IExtNullabilityInfo right )
            {
                Debug.Assert( left.Type.IsSZArray && left.ElementType != null );
                if( right.Type.IsSZArray )
                {
                    Debug.Assert( right.ElementType != null );
                    if( right.ElementType!.Type == left.ElementType.Type && right.IsNullable == left.IsNullable ) return right;
                    return ReconciliationError( monitor, left, right );
                }
                // Handle IReadOnlyList<> if possible.
                if( right.GenericTypeArguments.Count == 1
                    && left.ElementType.IsNullable == right.GenericTypeArguments[0].IsNullable
                    && right.Type.GetGenericTypeDefinition() == typeof( IReadOnlyList<> )
                    && right.Type.IsAssignableFrom( left.Type ) )
                {
                    return left;
                }
                return ReconciliationError( monitor, left, right );
            }

            static IExtNullabilityInfo? ReconciliationError( IActivityMonitor monitor, IExtNullabilityInfo left, IExtNullabilityInfo right )
            {
                monitor.Error( $"Unable to conciliate '{left.Type.ToCSharpName( false )}' and '{right.Type.ToCSharpName( false )}' types." );
                return null;
            }

            static IExtNullabilityInfo? UnsupportedTypeError( IActivityMonitor monitor, Type type )
            {
                monitor.Error( $"Not supported type: {type.ToCSharpName( false )}." );
                return null;
            }

            static Type? MapGeneric( Type t, out PocoTypeKind kind, out bool regular )
            {
                regular = false;
                kind = PocoTypeKind.List;
                if( t == typeof( IReadOnlyList<> ) ) return typeof( IList<> );
                if( t == typeof( IList<> ) ) return t;
                if( t == typeof( List<> ) )
                {
                    regular = true;
                    return t;
                }
                kind = PocoTypeKind.HashSet;
                if( t == typeof( IReadOnlySet<> ) ) return typeof( ISet<> );
                if( t == typeof( ISet<> ) ) return t;
                if( t == typeof( HashSet<> ) )
                {
                    regular = true;
                    return t;
                }
                kind = PocoTypeKind.Dictionary;
                if( t == typeof( IReadOnlyDictionary<,> ) ) return typeof( IDictionary<,> );
                if( t == typeof( IDictionary<,> ) ) return t;
                if( t == typeof( Dictionary<,> ) )
                {
                    regular = true;
                    return t;
                }
                return null;
            }
        }

    }
}
