using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CK.Setup;

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

    /// <summary>
    /// "Fake" builder: reference types are all nullable.
    /// </summary>
    /// <param name="type">The root type.</param>
    public ExtNullabilityInfo( Type type )
    {
        _type = type;
        _useReadState = true;
        _homogeneous = true;
        if( type.IsValueType )
        {
            if( type.IsGenericType )
            {
                var tGen = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();
                if( tGen == typeof( Nullable<> ) )
                {
                    _subTypes = new[] { new ExtNullabilityInfo( args[0], false ) };
                    _isNullable = true;
                }
                else
                {
                    _isNullable = false;
                    _subTypes = CreateFromArgs( args );
                }
            }
            else
            {
                _subTypes = Array.Empty<ExtNullabilityInfo>();
                _isNullable = false;
            }
        }
        else
        {
            _isNullable = true;
            if( type.IsArray )
            {
                _subTypes = new ExtNullabilityInfo( type.GetElementType()! );
            }
            else if( type.IsGenericType )
            {
                var args = CreateFromArgs( type.GetGenericArguments() );
                if( type.GetGenericTypeDefinition() == typeof( Dictionary<,> ) )
                {
                    args[0] = args[0].ToNonNullable();
                }
                _subTypes = args;
            }
            else
            {
                _subTypes = Array.Empty<ExtNullabilityInfo>();
            }
        }

        static ExtNullabilityInfo[] CreateFromArgs( Type[] args )
        {
            var s = new ExtNullabilityInfo[args.Length];
            for( int i = 0; i < args.Length; i++ )
            {
                s[i] = new ExtNullabilityInfo( args[i] );
            }
            return s;
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

    internal ExtNullabilityInfo( Type t, IExtNullabilityInfo a, bool isNullable )
    {
        Throw.DebugAssert( !t.IsValueType && ((t.IsGenericType && t.GenericTypeArguments.Length == 1) || t.IsSZArray) );
        _type = t;
        _subTypes = t.IsSZArray ? a : new[] { a };
        _isNullable = isNullable;
        _useReadState = true;
        _homogeneous = true;
    }

    internal ExtNullabilityInfo( Type t, IExtNullabilityInfo a0, IExtNullabilityInfo a1, bool isNullable )
    {
        Throw.DebugAssert( !t.IsValueType && t.IsGenericType && t.GetGenericTypeDefinition().GetGenericArguments().Length == 2 );
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

    internal ExtNullabilityInfo ToNonNullable()
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

    IExtNullabilityInfo IExtNullabilityInfo.ToNonNullable() => ToNonNullable();

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

    public IExtNullabilityInfo SetReferenceTypeDefinition( Type typeDefinition, bool? nullable )
    {
        Throw.CheckState( !Type.IsValueType && ElementType == null );
        Throw.CheckArgument( typeDefinition != null
                             && !typeDefinition.IsValueType
                             && typeDefinition.IsGenericTypeDefinition
                             && typeDefinition.GetGenericArguments().Length == GenericTypeArguments.Count );
        var t = typeDefinition.MakeGenericType( GenericTypeArguments.Select( a => a.Type ).ToArray() );
        return new ExtNullabilityInfo( t, _subTypes, nullable ?? _isNullable, _useReadState, _homogeneous );
    }

    public StringBuilder ToString( StringBuilder b )
    {
        if( _subTypes is ExtNullabilityInfo i )
        {
            i.ToString( b ).Append( "[]" );
        }
        else
        {
            b.Append( _type.Name );
            if( !_type.IsValueType && _subTypes is IExtNullabilityInfo[] a && a.Length > 0 )
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

}
