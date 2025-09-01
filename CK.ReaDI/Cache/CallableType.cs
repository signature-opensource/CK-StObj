using CK.Engine.TypeCollector;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace CK.Core;

public sealed class CallableType : IReaDIMethod
{
    readonly HandlerType _handler;
    readonly CachedMethod _method;
    readonly ImmutableArray<ParameterType> _parameters;
    internal CallableType? _next;
    int _monitorIdx;
    int _engineIdx;

    internal CallableType( HandlerType handler,
                           CachedMethod method,
                           ParameterType[] parameters )
    {
        _handler = handler;
        _method = method;
        _parameters = ImmutableCollectionsMarshal.AsImmutableArray( parameters );
        _next = handler.FirstCallable;
        handler._firstCallable = this;
    }

    ICachedType IReaDIMethod.Handler => _handler.Type;
     
    public CachedMethod Method => _method;

    public CallableType? NextCallable => _next;

    public ImmutableArray<ParameterType> Parameters => _parameters;

    internal void Initialize( int idxMonitor, int idxEngine )
    {
        _monitorIdx = idxMonitor;
        _engineIdx = idxEngine;
    }

    public override string ToString() => _method.ToStringWithDeclaringType();
}
