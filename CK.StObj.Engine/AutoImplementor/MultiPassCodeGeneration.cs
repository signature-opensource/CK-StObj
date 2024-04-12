using System;
using CK.Core;
using System.Reflection;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using CK.CodeGen;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates the result of the first run of code generation that requires one (or more) subsequent runs
    /// with the help of the same or a different method: container can be configured by previous passes required
    /// dependencies can use the <see cref="WaitForAttribute"/>.
    /// </summary>
    public abstract class MultiPassCodeGeneration
    {
        readonly object _owner;
        readonly MemberInfo? _target;
        readonly ITypeScope? _typeScope;
        MethodInfo? _currentMethod;
        ParameterInfo[]? _currentParameters;
        object?[]? _currentParameterValues;
        List<ParameterInfo>? _currentWaitingParameters;

        /// <summary>
        /// Executes the first pass of the code generation.
        /// </summary>
        /// <typeparam name="T">The type of the member to generate.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="owner">The targeted <see cref="IAutoImplementor{T}"/>.</param>
        /// <param name="context">The type generation context.</param>
        /// <param name="scope">The source code type scope to use to implement <paramref name="target"/>.</param>
        /// <param name="target">
        /// Method, property or type that must be implemented.
        /// This is null when <paramref name="owner"/> is <see cref="ICSCodeGenerator"/>: a global code generator has no specific target.
        /// </param>
        /// <returns>Whether the first pass succeeded and an subsequent pass to execute.</returns>
        public static (bool Success, MultiPassCodeGeneration? SecondPass) FirstPass<T>( IActivityMonitor monitor,
                                                                                        IAutoImplementor<T> owner,
                                                                                        ICSCodeGenerationContext context,
                                                                                        ITypeScope scope,
                                                                                        T target )
            where T : MemberInfo
        {
            var r = owner.Implement( monitor, target, context, scope );
            return HandleFirstResult( monitor, r, owner, scope, target );
        }

        /// <summary>
        /// Executes the first pass of the code generation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="owner">The <see cref="ICSCodeGenerator"/>.</param>
        /// <param name="context">The generation context.</param>
        /// <returns>Whether the first pass succeeded and an optional second pass to execute.</returns>
        public static (bool Success, MultiPassCodeGeneration? SecondPass) FirstPass( IActivityMonitor monitor,
                                                                                     ICSCodeGenerator owner,
                                                                                     ICSCodeGenerationContext context )
        {
            var r = owner.Implement( monitor, context );
            return HandleFirstResult<Type>( monitor, r, owner, null, null );
        }

        static (bool Success, MultiPassCodeGeneration? SecondPass) HandleFirstResult<T>( IActivityMonitor monitor,
                                                                                         CSCodeGenerationResult r,
                                                                                         object first,
                                                                                         ITypeScope? scope,
                                                                                         T? toImplement ) where T : MemberInfo
        {
            Throw.DebugAssert( first is ICSCodeGenerator || first is IAutoImplementor<T> );
            Throw.DebugAssert( (first is ICSCodeGenerator) == (toImplement == null) );
            if( r.IsSuccess )
            {
                return (true, null);
            }
            if( r.HasError )
            {
                monitor.Fatal( $"'{first.GetType().Name}.Implement' failed." );
                return (false, null);
            }
            Throw.DebugAssert( r.IsRetry || r.MethodName != null );
            MethodInfo? implementor = null;
            if( r.MethodName != null )
            {
                implementor = FindSingleMethod( monitor, first.GetType(), r.MethodName );
                if( implementor == null ) return (false, null);
            }
            return toImplement switch
            {
                null => (true, new CodeGeneratorResult( (ICSCodeGenerator)first, implementor )),
                Type t => (true, new TypeResult( (IAutoImplementor<Type>)first, scope!, implementor, t )),
                MethodInfo m => (true, new MethodResult( (IAutoImplementor<MethodInfo>)first, scope!, implementor, m )),
                PropertyInfo p => (true, new PropertyResult( (IAutoImplementor<PropertyInfo>)first, scope!, implementor, p )),
                _ => throw new NotSupportedException()
            };
        }

        static MethodInfo? FindSingleMethod( IActivityMonitor monitor, Type caller, string methodName )
        {
            var mA = caller.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ).Where( m => m.Name == methodName && m.DeclaringType != typeof( object ) ).ToList();
            if( mA.Count == 0 )
            {
                monitor.Fatal( $"'{caller.Name}.Implement' wants to use its method named '{methodName}' but no such method can be found." );
                return null;
            }
            if( mA.Count > 1 )
            {
                monitor.Fatal( $"'{caller.Name}.Implement' wants to use a method named '{methodName}' but this method must be unique. Found {mA.Count} overloads." );
                return null;
            }
            return mA[0]!;
        }

        private protected object Owner => _owner;
        private protected MemberInfo? Target => _target;
        [MemberNotNullWhen( false, nameof( _target ), nameof( _typeScope ) )]
        bool IsCodeGenerator => _target == null;

        /// <summary>
        /// Gets the target name.
        /// This is the empty string when <see cref="IsCodeGenerator"/> is true.
        /// </summary>
        private protected abstract string TargetName { get; }

        /// <summary>
        /// Gets the implementor name.
        /// </summary>
        public string ImplementorName => $"Method '{_owner.GetType().Name}.{(_currentMethod != null ? _currentMethod.Name : "Implement")}";

        [MemberNotNullWhen( true, nameof( _currentWaitingParameters ) )]
        bool HasWaitingServices => _currentWaitingParameters != null && _currentWaitingParameters.Count > 0;

        bool DumpAllErrorServiceResolution( IActivityMonitor monitor )
        {
            if( _currentWaitingParameters == null ) return false;
            foreach( var p in _currentWaitingParameters )
            {
                DumpErrorServiceResolution( monitor, p );
            }
            return true;
        }

        void DumpErrorServiceResolution( IActivityMonitor monitor, ParameterInfo p )
        {
            monitor.Error( $"{_owner.GetType():N}.{p.Member.Name}: parameter {p.Name} cannot be resolved: type '{p.ParameterType:C}' not available." );
        }


        /// <summary>
        /// Executes the second pass by either.
        /// <para>
        /// On error, a fatal or error message has necessarily been logged.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="context">The code generation context.</param>
        /// <param name="passes">The result of the first pass.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool RunSecondPass( IActivityMonitor monitor, ICSCodeGenerationContext context, List<MultiPassCodeGeneration> passes )
        {
            if( passes.Count > 0 )
            {
                int passNumber = 0;
                for(; ; )
                {
                    List<MultiPassCodeGeneration>? next = null;
                    bool madeProgress = false;
                    using( monitor.OpenInfo( $"Running code generation pass n°{++passNumber} with {passes.Count} code generators." ) )
                    {
                        foreach( var s in passes )
                        {
                            RunResult r = s.RunSecondPass( monitor, context );
                            if( r == RunResult.Failed ) return false;
                            if( r == RunResult.Success )
                            {
                                madeProgress = true;
                            }
                            else
                            {
                                next ??= new List<MultiPassCodeGeneration>();
                                next.Add( s );
                                // Retry doesn't count as a progress.
                                madeProgress |= r == RunResult.MadeProgress;
                            }
                        }
                    }
                    // No next, we are done.
                    if( next == null ) break;
                    // If we didn't make progress, this is an error.
                    if( !madeProgress )
                    {
                        foreach( var p in next )
                        {
                            madeProgress |= p.DumpAllErrorServiceResolution( monitor );
                        }
                        // No dump of waiting services: it is one or more Retry that are not satisfied.
                        if( !madeProgress )
                        {
                            monitor.Error( $"Stopping since {next.Count} pass are retrying without any progress:{Environment.NewLine}" +
                                           $"{next.Select( p => p.ImplementorName ).Concatenate()}" );
                        }
                        return false;
                    }
                    passes = next;
                }
            }
            return true;
        }

        enum RunResult
        {
            Failed,
            Success,
            Retry,
            MadeProgress
        }

        RunResult RunSecondPass( IActivityMonitor monitor, ICSCodeGenerationContext context )
        {
            RunResult result;
            try
            {
                object? o;
                if( _currentMethod != null )
                {
                    if( _currentParameters == null )
                    {
                        result = InitializeMethodParameters( monitor, context );
                        if( result != RunResult.Success ) return result;
                    }
                    else if( HasWaitingServices )
                    {
                        result = FillWaitingMethodParameters( monitor, context );
                        if( result != RunResult.Success ) return result;
                    }
                    o = _currentMethod.Invoke( _owner, _currentParameterValues );
                }
                else
                {
                    o = CallImplementorTypeMethod( monitor, context );
                }
                CSCodeGenerationResult r;
                if( o is CSCodeGenerationResult genResult ) r = genResult;
                else if( o is bool b ) r = b ? CSCodeGenerationResult.Success : CSCodeGenerationResult.Failed;
                else
                {
                    monitor.Fatal( $"{ImplementorName} returned an unhandled value: '{o}'.{Environment.NewLine}" +
                                   $"Expected non null CSCodeGenerationResult or bool value." );
                    return RunResult.Failed;
                }
                if( r.HasError )
                {
                    monitor.Fatal( IsCodeGenerator ? $"{ImplementorName} failed." : $"{ImplementorName} failed to implement {TargetName}." );
                    return RunResult.Failed;
                }
                if( r.IsRetry )
                {
                    return RunResult.Retry;
                }
                if( r.MethodName != null )
                {
                    _currentMethod = FindSingleMethod( monitor, _owner.GetType(), r.MethodName );
                    if( _currentMethod == null ) return RunResult.Failed;
                    _currentParameters = null;
                    return RunResult.MadeProgress;
                }
                Throw.DebugAssert( r.IsSuccess );
                return RunResult.Success;
            }
            catch( Exception ex )
            {
                monitor.Fatal( IsCodeGenerator ? $"{ImplementorName} failed." : $"{ImplementorName} failed to implement {TargetName}.", ex );
                return RunResult.Failed;
            }
        }

        RunResult InitializeMethodParameters( IActivityMonitor monitor, ICSCodeGenerationContext context )
        {
            Throw.DebugAssert( _currentMethod != null );
            _currentParameters = _currentMethod.GetParameters();
            _currentParameterValues = new object?[_currentParameters.Length];
            _currentWaitingParameters?.Clear();
            for( int i = 0; i < _currentParameters.Length; ++i )
            {
                var p = _currentParameters[i];
                if( typeof( IActivityMonitor ) == p.ParameterType ) _currentParameterValues[i] = monitor;
                else if( typeof( ICSCodeGenerationContext ) == p.ParameterType ) _currentParameterValues[i] = context;
                else if( typeof( MemberInfo ).IsAssignableFrom( p.ParameterType ) ) _currentParameterValues[i] = Target;
                else if( typeof( ITypeScope ) == p.ParameterType ) _currentParameterValues[i] = _typeScope;
                else
                {
                    var s = context.CurrentRun.ServiceContainer.GetService( p.ParameterType );
                    if( s != null )
                    {
                        _currentParameterValues[i] = s;
                    }
                    else
                    {
                        if( p.HasDefaultValue )
                        {
                            _currentParameterValues[i] = p.DefaultValue;
                        }
                        else
                        {
                            if( p.GetCustomAttributesData().Any( a => a.AttributeType.Name == nameof( WaitForAttribute ) ) )
                            {
                                _currentWaitingParameters ??= new List<ParameterInfo>();
                                _currentWaitingParameters.Add( p );
                            }
                            else
                            {
                                DumpErrorServiceResolution( monitor, p );
                                return RunResult.Failed;
                            }
                        }
                    }
                }
            }
            return HasWaitingServices ? RunResult.MadeProgress : RunResult.Success;
        }

        RunResult FillWaitingMethodParameters( IActivityMonitor monitor, ICSCodeGenerationContext context )
        {
            Throw.DebugAssert( _currentParameters != null );
            RunResult result = RunResult.Retry;
            Throw.DebugAssert( HasWaitingServices && _currentParameterValues != null );
            for( int i = _currentWaitingParameters.Count-1; i >= 0; i-- )
            {
                var p = _currentWaitingParameters[i];
                var s = context.CurrentRun.ServiceContainer.GetService( p.ParameterType );
                if( s != null )
                {
                    _currentParameterValues[Array.IndexOf( _currentParameters, p )] = s;
                    result = RunResult.MadeProgress;
                    _currentWaitingParameters.RemoveAt( i );
                }
                --i;
            }
            if( _currentWaitingParameters.Count == 0 ) result = RunResult.Success;
            return result;
        }


        private protected MultiPassCodeGeneration( object firstRunner, ITypeScope? s, MethodInfo? implementor, MemberInfo? target )
        {
            _owner = firstRunner;
            _typeScope = s;
            _currentMethod = implementor;
            _target = target;
        }

        private protected abstract CSCodeGenerationResult CallImplementorTypeMethod( IActivityMonitor monitor,
                                                                                     ICSCodeGenerationContext context );

        sealed class CodeGeneratorResult : MultiPassCodeGeneration
        {
            public CodeGeneratorResult( ICSCodeGenerator first, MethodInfo? implementor )
                : base( first, null, implementor, null )
            {
            }

            public new ICSCodeGenerator Owner => (ICSCodeGenerator)base.Owner;

            private protected override string TargetName => String.Empty;

            private protected override CSCodeGenerationResult CallImplementorTypeMethod( IActivityMonitor monitor,
                                                                                         ICSCodeGenerationContext context )
            {
                return Owner.Implement( monitor, context );
            }
        }

        abstract class GenericLayer<T> : MultiPassCodeGeneration where T : MemberInfo
        {
            private protected GenericLayer( IAutoImplementor<T> firstRunner, ITypeScope s, MethodInfo? implementor, T target )
                : base( firstRunner, s, implementor, target )
            {
            }

            public new T Target => (T)base.Target!;

            public new IAutoImplementor<T> Owner => (IAutoImplementor<T>)base.Owner;

            private protected override CSCodeGenerationResult CallImplementorTypeMethod( IActivityMonitor monitor,
                                                                                         ICSCodeGenerationContext context )
            {
                Throw.DebugAssert( !IsCodeGenerator && _typeScope != null );
                return Owner.Implement( monitor, Target, context, _typeScope );
            }
        }

        sealed class MethodResult : GenericLayer<MethodInfo>
        {
            public MethodResult( IAutoImplementor<MethodInfo> first, ITypeScope s, MethodInfo? implementor, MethodInfo target )
                : base( first, s, implementor, target )
            {
            }

            private protected override string TargetName => Target.DeclaringType!.FullName + '.' + Target.Name;
        }

        sealed class PropertyResult : GenericLayer<PropertyInfo>
        {
            public PropertyResult( IAutoImplementor<PropertyInfo> first, ITypeScope s, MethodInfo? implementor, PropertyInfo m )
                : base( first, s, implementor, m )
            {
            }

            private protected override string TargetName => Target.DeclaringType!.FullName + '.' + Target.Name;
        }

        sealed class TypeResult : GenericLayer<Type>
        {
            public TypeResult( IAutoImplementor<Type> first, ITypeScope s, MethodInfo? implementor, Type target )
                : base( first, s, implementor, target )
            {
            }

            private protected override string TargetName => Target.FullName!;
        }

    }


}





