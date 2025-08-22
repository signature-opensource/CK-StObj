using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Engine.TypeCollector;

sealed class CachedMethodInfo : CachedMethodBase, ICachedMethodInfo
{
    CachedParameterInfo? _returnParameterInfo;
    MethodInfo? _baseMethodDefinition;

    internal CachedMethodInfo( ICachedType declaringType, MethodInfo method )
        : base( declaringType, method )
    {
    }


    public bool IsStatic => MethodInfo.IsStatic;

    public MethodInfo MethodInfo => Unsafe.As<MethodInfo>( _member );

    public CachedParameterInfo ReturnParameterInfo => _returnParameterInfo ??= new CachedParameterInfo( this, MethodInfo.ReturnParameter );

    MethodInfo? BaseMethodDefinition => _baseMethodDefinition ??= MethodInfo.GetBaseDefinition();

    public bool IsOverride => BaseMethodDefinition != _member;

    public bool IsAsynchronous
    {
        get
        {
            var r = ReturnParameterInfo.ParameterType;
            var knownTypes = TypeCache.KnownTypes;
            return r == knownTypes.Task
                   || r == knownTypes.ValueTask
                   || (r.IsGenericType && (r.GenericTypeDefinition == knownTypes.GenericTaskDefinition
                                           || r.GenericTypeDefinition == knownTypes.GenericValueTaskDefinition));
        }
    }

    public ICachedType? GetAsynchronousReturnedType()
    {
        var r = ReturnParameterInfo.ParameterType;
        var knownTypes = TypeCache.KnownTypes;
        if( r == knownTypes.Task || r == knownTypes.ValueTask )
        {
            return knownTypes.Void;
        }
        if( r.IsGenericType && (r.GenericTypeDefinition == knownTypes.GenericTaskDefinition
                                || r.GenericTypeDefinition == knownTypes.GenericValueTaskDefinition) )
        {
            return r.GenericArguments[0];
        }
        return null;
    }

    public override StringBuilder Write( StringBuilder b, bool withDeclaringType )
    {
        if( MethodInfo.IsStatic ) b.Append( "static " );
        ReturnParameterInfo.Write( b );
        b.Append( ' ' );
        if( withDeclaringType ) b.Append( DeclaringType.CSharpName ).Append( '.' );
        b.Append( Name ).Append('(');
        int i = 0;
        foreach( var p in ParameterInfos )
        {
            if( i++ > 0 ) b.Append( ',' );
            b.Append( ' ' );
            p.Write( b );
        }
        if( i > 0 ) b.Append( ' ' );
        b.Append( ')' );
        return b;
    }
}
