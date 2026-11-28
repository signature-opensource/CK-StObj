using CK.Core;
using CK.TypeCache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// The slots of the engine run, in their order. The <see cref="AppGenContext"/> slots are called for each App, between
/// the <see cref="Gen"/> and the <see cref="EndingGen"/> (or <see cref="OnErrorGen"/>) slots.
/// </summary>
enum HandlerSlot
{
    StartingGen,
    Gen,
    EndingGen,
    OnErrorGen,
    StartingApp,
    App,
    EndingApp,
    OnErrorApp
}

/// <summary>
/// The handler methods of a handler type by slot, computed once per type.
/// <para>
/// A handler method is a public instance method with at least one context parameter (a context or a marker of a
/// context). Its slot is given by its marker (<see cref="Starting{T}"/>, <see cref="Ending{T}"/>, <see cref="OnError{T}"/>)
/// or, without a marker, by its innermost context. Its other parameters can be an <see cref="IActivityMonitor"/> and the
/// contexts that exist in the slot (the <see cref="GenContext"/> in an App slot). It returns void: an error logged
/// (or an exception) is a failure.
/// </para>
/// </summary>
sealed class HandlerType
{
    internal enum Arg
    {
        Monitor,
        Gen,
        App,
        StartingGen,
        EndingGen,
        OnErrorGen,
        StartingApp,
        EndingApp,
        OnErrorApp
    }

    internal readonly struct Method
    {
        public Method( MethodInfo info, Arg[] args )
        {
            Info = info;
            Args = args;
        }

        public MethodInfo Info { get; }

        public Arg[] Args { get; }

        public object?[] CreateArguments( IActivityMonitor monitor, GenContext gen, AppGenContext? app )
        {
            var a = new object?[Args.Length];
            for( int i = 0; i < a.Length; i++ )
            {
                a[i] = Args[i] switch
                {
                    Arg.Monitor => monitor,
                    Arg.Gen => gen,
                    Arg.App => app,
                    Arg.StartingGen => new Starting<GenContext>( gen ),
                    Arg.EndingGen => new Ending<GenContext>( gen ),
                    Arg.OnErrorGen => new OnError<GenContext>( gen ),
                    Arg.StartingApp => new Starting<AppGenContext>( app! ),
                    Arg.EndingApp => new Ending<AppGenContext>( app! ),
                    Arg.OnErrorApp => new OnError<AppGenContext>( app! ),
                    _ => throw new NotSupportedException()
                };
            }
            return a;
        }

        public override string ToString() => $"{Info.DeclaringType?.ToCSharpName()}.{Info.Name}";
    }

    readonly Method[][] _slots;

    HandlerType( Method[][] slots )
    {
        _slots = slots;
        IsEmpty = slots.All( s => s.Length == 0 );
    }

    /// <summary>
    /// Gets whether this type has no handler method.
    /// </summary>
    public bool IsEmpty { get; }

    /// <summary>
    /// Gets the methods of a slot.
    /// </summary>
    public Method[] this[HandlerSlot slot] => _slots[(int)slot];

    /// <summary>
    /// Analyzes a handler type. Errors are logged.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="t">The handler type.</param>
    /// <returns>The handler type or null on error.</returns>
    internal static HandlerType? Create( IActivityMonitor monitor, Type t )
    {
        var slots = new List<Method>?[8];
        bool success = true;
        foreach( var m in t.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static ) )
        {
            // The members of the EngineAttributeImpl bases are not handler methods.
            if( m.IsSpecialName
                || m.DeclaringType == typeof( object )
                || m.DeclaringType?.Assembly == typeof( EngineAttributeImpl ).Assembly )
            {
                continue;
            }
            var parameters = m.GetParameters();
            var args = new Arg?[parameters.Length];
            bool isHandler = false;
            for( int i = 0; i < parameters.Length; i++ )
            {
                var a = ToArg( parameters[i] );
                args[i] = a;
                isHandler |= a.HasValue && a.Value != Arg.Monitor;
            }
            if( !isHandler ) continue;
            if( !TryGetSlot( monitor, t, m, parameters, args, out var slot ) )
            {
                success = false;
                continue;
            }
            (slots[(int)slot] ??= new List<Method>()).Add( new Method( m, args.Select( a => a!.Value ).ToArray() ) );
        }
        return success
                ? new HandlerType( slots.Select( s => s?.ToArray() ?? Array.Empty<Method>() ).ToArray() )
                : null;
    }

    static bool TryGetSlot( IActivityMonitor monitor, Type t, MethodInfo m, ParameterInfo[] parameters, Arg?[] args, out HandlerSlot slot )
    {
        slot = default;
        string? error = null;
        if( m.IsStatic ) error = "it must be an instance method";
        else if( m.ReturnType != typeof( void ) ) error = "it must return void (an error logged is a failure)";
        else if( m.IsGenericMethodDefinition ) error = "it must not be generic";
        else
        {
            int iUnknown = Array.FindIndex( args, a => !a.HasValue );
            if( iUnknown >= 0 )
            {
                error = $"its parameter '{parameters[iUnknown].Name}' can't be provided: only an IActivityMonitor, a GenContext, an AppGenContext and the Starting<T>, Ending<T> or OnError<T> of a context are supported";
            }
            else
            {
                var markers = args.Where( a => a!.Value >= Arg.StartingGen ).Select( a => a!.Value ).ToArray();
                if( markers.Length > 1 )
                {
                    error = "it can have at most one Starting<T>, Ending<T> or OnError<T> parameter";
                }
                else
                {
                    slot = markers.Length == 1
                            ? markers[0] switch
                            {
                                Arg.StartingGen => HandlerSlot.StartingGen,
                                Arg.EndingGen => HandlerSlot.EndingGen,
                                Arg.OnErrorGen => HandlerSlot.OnErrorGen,
                                Arg.StartingApp => HandlerSlot.StartingApp,
                                Arg.EndingApp => HandlerSlot.EndingApp,
                                _ => HandlerSlot.OnErrorApp
                            }
                            : args.Contains( Arg.App ) ? HandlerSlot.App : HandlerSlot.Gen;
                    if( slot < HandlerSlot.StartingApp && args.Contains( Arg.App ) )
                    {
                        error = "an AppGenContext doesn't exist in a GenContext slot";
                    }
                }
            }
        }
        if( error != null )
        {
            monitor.Error( $"Invalid engine handler method '{t.ToCSharpName()}.{m.Name}': {error}." );
            return false;
        }
        return true;
    }

    static Arg? ToArg( ParameterInfo p )
    {
        var t = p.ParameterType;
        if( t.IsByRef )
        {
            // An "in" parameter is fine.
            if( !p.IsIn ) return null;
            t = t.GetElementType()!;
        }
        if( t == typeof( IActivityMonitor ) ) return Arg.Monitor;
        if( t == typeof( GenContext ) ) return Arg.Gen;
        if( t == typeof( AppGenContext ) ) return Arg.App;
        if( t == typeof( Starting<GenContext> ) ) return Arg.StartingGen;
        if( t == typeof( Ending<GenContext> ) ) return Arg.EndingGen;
        if( t == typeof( OnError<GenContext> ) ) return Arg.OnErrorGen;
        if( t == typeof( Starting<AppGenContext> ) ) return Arg.StartingApp;
        if( t == typeof( Ending<AppGenContext> ) ) return Arg.EndingApp;
        if( t == typeof( OnError<AppGenContext> ) ) return Arg.OnErrorApp;
        return null;
    }
}
