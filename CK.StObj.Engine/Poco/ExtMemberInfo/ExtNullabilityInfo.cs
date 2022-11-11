using CK.Core;
using CK.Core.Impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

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

    }

    //class Merger
    //{
    //    public Merger( ExtNullabilityInfo left )
    //    {
    //        Throw.CheckNotNullArgument( left );
    //        Left = left;
    //    }

    //    internal ExtNullabilityInfo Left { get; private set; }

    //    public bool Merge( ExtNullabilityInfo right )
    //    {
    //        Throw.CheckNotNullArgument( right );
    //        var r = Merge( Left, right );
    //        if( r != null )
    //        {
    //            Left = r;
    //            return true;
    //        }
    //        return false;
    //    }

    //    ExtNullabilityInfo? Merge( IExtNullabilityInfo left, IExtNullabilityInfo right )
    //    {
    //        var leftElementType = left.ElementType;
    //        var rightElementType = right.ElementType;
    //        if( leftElementType != null )
    //        {
    //            if( rightElementType != null ) return MergeArray( left, leftElementType, right, rightElementType );
    //            return MergeArrayAndType( left, leftElementType, right );
    //        }
    //        if( rightElementType != null )
    //        {
    //            return MergeTypeAndArray( left, right, rightElementType );
    //        }
    //        return MergeTypeAndType( left, right );
    //    }

    //    protected virtual ExtNullabilityInfo? MergeArray( IExtNullabilityInfo left,
    //                                                      IExtNullabilityInfo leftElementType,
    //                                                      IExtNullabilityInfo right,
    //                                                      IExtNullabilityInfo rightElementType )
    //    {
    //        var item = Merge( leftElementType, rightElementType );
    //    }
    //}
}
