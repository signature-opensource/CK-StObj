using System;
using CK.Core;
using System.Reflection;
using System.Linq;
using System.Diagnostics;
using CK.Reflection;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using CK.CodeGen;
using System.Collections.Generic;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates the result of the first run of code generation that requires one (or more) subsequent runs
    /// with the help of a new instance or a method: dependency injection can be configured during the first pass
    /// and <see cref="Implementor"/> can use any required dependencies.
    /// </summary>
    public abstract class MultiPassCodeGeneration
    {
        /// <summary>
        /// Gets the <see cref="IAutoImplementorMethod"/>, <see cref="IAutoImplementorProperty"/>, <see cref="IAutoImplementorType"/>
        /// or <see cref="ICodeGenerator"/> that initiated this second pass.
        /// </summary>
        public object FirstRunner { get; }

        /// <summary>
        /// Gets whether this is a global <see cref="ICodeGenerator"/> or a targeted <see cref="IAutoImplementor{T}"/> implementor.
        /// </summary>
        [MemberNotNullWhen( false, nameof(Target) )]
        public bool IsCodeGenerator => Target == null;

        /// <summary>
        /// Gets the Type or the Method that will execute the second generation pass.
        /// </summary>
        public readonly MemberInfo Implementor;

        /// <summary>
        /// Gets the source code type scope to use.
        /// This is null when <see cref="IsCodeGenerator"/> is true: a global code generator has no specific target.
        /// </summary>
        public readonly ITypeScope? TypeScope;

        /// <summary>
        /// Gets the method, property or type that must be implemented.
        /// This is null when <see cref="IsCodeGenerator"/> is true: a global code generator has no specific target.
        /// </summary>
        public readonly MemberInfo? Target;

        /// <summary>
        /// Gets the target name.
        /// This is the empty string when <see cref="IsCodeGenerator"/> is true.
        /// </summary>
        public abstract string TargetName { get; }

        /// <summary>
        /// Gets the implementor name.
        /// </summary>
        public string ImplementorName => Implementor is MethodInfo m
                                            ? $"Method '{m.DeclaringType!.Name}.{m.Name}"
                                            : (IsCodeGenerator ? $"Code generator '{((Type)Implementor).FullName}'" : $"Implementor '{((Type)Implementor).FullName}'");

        // Subsequent runs on named Methods are tracked here.
        object? _secondRunner;
        MethodInfo? _subsequentRun;

        /// <summary>
        /// Executes the second pass by either:
        /// <list type="bullet">
        ///     <item>If <see cref="Implementor"/> is a Type: instantiating it and executing its <see cref="IAutoImplementor{T}.Implement"/> method.</item>
        ///     <item>If <see cref="Implementor"/> is a MethodInfo: calling it.</item>
        /// </list>
        /// Subsequent runs can use methods as long as a <see cref="AutoImplementationResult(string)"/> is returned by implemetors.
        /// On error, a fatal or error message has necessarily been logged.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="context">The code generation context.</param>
        /// <param name="passes">The result of the first pass.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool RunSecondPass( IActivityMonitor monitor, ICodeGenerationContext context, List<MultiPassCodeGeneration> passes )
        {
            if( passes.Count > 0 )
            {
                int passNumber = 0;
                for(; ; )
                {
                    List<MultiPassCodeGeneration>? next = null;
                    using( monitor.OpenInfo( $"Running code generation pass n°{++passNumber} with {passes.Count} code generators." ) )
                    {
                        foreach( var s in passes )
                        {
                            if( !s.RunSecondPass( monitor, context ) )
                            {
                                return false;
                            }
                            if( s._subsequentRun != null )
                            {
                                if( next == null ) next = new List<MultiPassCodeGeneration>();
                                next.Add( s );
                            }
                        }
                    }
                    if( next == null ) break;
                    passes = next;
                }
            }
            return true;
        }

        bool RunSecondPass( IActivityMonitor monitor, ICodeGenerationContext context )
        {
            try
            {
                AutoImplementationResult r;
                if( Implementor is Type iType )
                {
                    Debug.Assert( _secondRunner == null );
                    object? impl;
                    using( var s = new SimpleServiceContainer( context.CurrentRun.ServiceContainer ) )
                    {
                        // This enables the implementor type to resolve its creator.
                        s.Add( FirstRunner );
                        s.Add( FirstRunner.GetType(), FirstRunner );
                        impl = s.SimpleObjectCreate( monitor, iType );
                        if( impl == null )
                        {
                            monitor.Fatal( $"Failed to instantiate '{ImplementorName}' type." );
                            return false;
                        }
                    }
                    _secondRunner = impl;
                    r = CallImplementorTypeMethod( monitor, context, impl );
                }
                else
                {
                    MethodInfo m;
                    if( _secondRunner == null )
                    {
                        Debug.Assert( _subsequentRun == null );
                        _secondRunner = FirstRunner;
                        m = (MethodInfo)Implementor;
                    }
                    else
                    {
                        Debug.Assert( _subsequentRun != null );
                        m = _subsequentRun;
                        _subsequentRun = null;
                    }
                    var parameters = m.GetParameters();
                    var values = new object?[parameters.Length];
                    for( int i = 0; i < parameters.Length; ++i )
                    {
                        var p = parameters[i];
                        if( typeof( IActivityMonitor ) == p.ParameterType ) values[i] = monitor;
                        else if( typeof( ICodeGenerationContext ) == p.ParameterType ) values[i] = context;
                        else if( typeof( MemberInfo ).IsAssignableFrom( p.ParameterType ) ) values[i] = Target;
                        else if( typeof( ITypeScope ) == p.ParameterType ) values[i] = TypeScope;
                        else
                        {
                            values[i] = p.HasDefaultValue
                                            ? (context.CurrentRun.ServiceContainer.GetService( p.ParameterType ) ?? p.DefaultValue)
                                            : context.CurrentRun.ServiceContainer.GetRequiredService( p.ParameterType );
                        }
                    }
                    object? o = m.Invoke( _secondRunner, values );
                    if( o == null ) r = AutoImplementationResult.Success;
                    else if( o is AutoImplementationResult ) r = (AutoImplementationResult)o;
                    else if( o is bool b ) r = b ? AutoImplementationResult.Success : AutoImplementationResult.Failed;
                    else
                    {
                        monitor.Fatal( $"{ImplementorName} returned an unhandled type of object: {o}." );
                        return false;
                    }
                }
                if( r.HasError )
                {
                    monitor.Fatal( IsCodeGenerator ? $"{ImplementorName} failed." : $"{ImplementorName} failed to implement {TargetName}." );
                    return false;
                }
                if( r.ImplementorType != null )
                {
                    monitor.Fatal( $"{ImplementorName}: the implement method must not return another type to instantiate (but can return one of its method name to call)." );
                    return false;
                }
                if( r.MethodName != null )
                {
                    MethodInfo? next = FindSingleMethod( monitor, _secondRunner.GetType(), r.MethodName );
                    if( next == null ) return false;
                    _subsequentRun = next;
                }
                return true;
            }
            catch( Exception ex )
            {
                monitor.Fatal( IsCodeGenerator ? $"{ImplementorName} failed." : $"{ImplementorName} failed to implement {TargetName}.", ex );
                return false;
            }
        }

        /// <summary>
        /// Executes the first pass of the code generation.
        /// </summary>
        /// <typeparam name="T">The type of the member to generate.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="first">The initial auto implementor.</param>
        /// <param name="context">The type generation context.</param>
        /// <param name="scope">See <see cref="TypeScope"/>.</param>
        /// <param name="toImplement">See <see cref="Target"/>.</param>
        /// <returns>Whether the first pass succeeded and an optional second pass to execute.</returns>
        public static (bool Success, MultiPassCodeGeneration? SecondPass) FirstPass<T>( IActivityMonitor monitor,
                                                                                        IAutoImplementor<T> first,
                                                                                        ICodeGenerationContext context,
                                                                                        ITypeScope scope,
                                                                                        T toImplement )
            where T : MemberInfo
        {
            var r = first.Implement( monitor, toImplement, context, scope );
            return HandleFirstResult<T>( monitor, r, first, context, scope, toImplement );
        }

        /// <summary>
        /// Executes the first pass of the code generation.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="first">The initial auto implementor.</param>
        /// <param name="context">The generation context.</param>
        /// <returns>Whether the first pass succeeded and an optional second pass to execute.</returns>
        public static (bool Success, MultiPassCodeGeneration? SecondPass) FirstPass( IActivityMonitor monitor,
                                                                                      ICodeGenerator first,
                                                                                      ICodeGenerationContext context )
        {
            var r = first.Implement( monitor, context );
            return HandleFirstResult<Type>( monitor, r, first, context, null, null );
        }

        static (bool Success, MultiPassCodeGeneration? SecondPass) HandleFirstResult<T>( IActivityMonitor monitor, AutoImplementationResult r, object first, ICodeGenerationContext context, ITypeScope? scope, T? toImplement ) where T : MemberInfo
        {
            Debug.Assert( first is ICodeGenerator || first is IAutoImplementor<T> );
            Debug.Assert( (first is ICodeGenerator) == (toImplement == null) );
            if( r.HasError )
            {
                monitor.Fatal( $"'{first.GetType().Name}.Implement' failed." );
                return (false, null);
            }
            MemberInfo? implementor = null;
            if( r.ImplementorType != null )
            {
                if( toImplement == null )
                {
                    if( !(typeof( ICodeGenerator ).IsAssignableFrom( r.ImplementorType )) )
                    {
                        monitor.Fatal( $"'{first.GetType().Name}.Implement' asked to use type '{r.ImplementorType}' that is not a ICodeGenerator." );
                        return (false, null);
                    }
                }
                else if( !(typeof( IAutoImplementor<T> ).IsAssignableFrom( r.ImplementorType )) )
                {
                    monitor.Fatal( $"'{first.GetType().Name}.Implement' asked to use type '{r.ImplementorType}' that is not a IAutoImplementor<{typeof( T ).Name}>." );
                    return (false, null);
                }
                implementor = r.ImplementorType;
            }
            else if( r.MethodName != null )
            {
                implementor = FindSingleMethod( monitor, first.GetType(), r.MethodName );
                if( implementor == null ) return (false, null);
            }
            if( implementor != null )
            {
                return toImplement switch
                {
                    null => (true, new CodeGeneratorResult( (ICodeGenerator)first, implementor )),
                    Type t => (true, new TypeResult( (IAutoImplementor<Type>)first, scope!, implementor, t )),
                    MethodInfo m => (true, new MethodResult( (IAutoImplementor<MethodInfo>)first, scope!, implementor, m )),
                    PropertyInfo p => (true, new PropertyResult( (IAutoImplementor<PropertyInfo>)first, scope!, implementor, p )),
                    _ => throw new NotSupportedException()
                };
            }
            return (true, null);
        }

        static MethodInfo? FindSingleMethod( IActivityMonitor monitor, Type caller, string methodName )
        {
            var mA = caller.GetMethods( BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ).Where( m => m.Name == methodName && m.DeclaringType != typeof(object) ).ToList();
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

        private protected MultiPassCodeGeneration( object firstRunner, ITypeScope? s, MemberInfo implementor, MemberInfo? target )
        {
            FirstRunner = firstRunner;
            TypeScope = s;
            Implementor = implementor;
            Target = target;
        }

        private protected abstract AutoImplementationResult CallImplementorTypeMethod( IActivityMonitor monitor, ICodeGenerationContext context, object impl );

        sealed class CodeGeneratorResult : MultiPassCodeGeneration
        {
            public CodeGeneratorResult( ICodeGenerator first, MemberInfo implementor )
                : base( first, null, implementor, null )
            {
            }

            public override string TargetName => String.Empty;

            private protected override AutoImplementationResult CallImplementorTypeMethod( IActivityMonitor monitor, ICodeGenerationContext context, object impl )
            {
                return ((ICodeGenerator)impl).Implement( monitor, context );
            }
        }

        abstract class GenericLayer<T> : MultiPassCodeGeneration where T : MemberInfo
        {
            private protected GenericLayer( IAutoImplementor<T> firstRunner, ITypeScope s, MemberInfo implementor, T target )
                : base( firstRunner, s, implementor, target )
            {
            }

            public new T Target => (T)base.Target!;

            public new IAutoImplementor<T> FirstRunner => (IAutoImplementor<T>)base.FirstRunner;

            private protected override AutoImplementationResult CallImplementorTypeMethod( IActivityMonitor monitor, ICodeGenerationContext context, object impl )
            {
                Debug.Assert( !IsCodeGenerator && TypeScope != null );
                Debug.Assert( impl is IAutoImplementor<T>, "This has been already tested." );
                return ((IAutoImplementor<T>)impl).Implement( monitor, Target, context, TypeScope );
            }
        }

        sealed class MethodResult : GenericLayer<MethodInfo>
        {
            public MethodResult( IAutoImplementor<MethodInfo> first, ITypeScope s, MemberInfo implementor, MethodInfo target )
                : base( first, s, implementor, target )
            {
            }

            public override string TargetName => Target.DeclaringType!.FullName + '.' + Target.Name;
        }

        sealed class PropertyResult : GenericLayer<PropertyInfo>
        {
            public PropertyResult( IAutoImplementor<PropertyInfo> first, ITypeScope s, MemberInfo implementor, PropertyInfo m )
                : base( first, s, implementor, m )
            {
            }

            public override string TargetName => Target.DeclaringType!.FullName + '.' + Target.Name;
        }

        sealed class TypeResult : GenericLayer<Type>
        {
            public TypeResult( IAutoImplementor<Type> first, ITypeScope s, MemberInfo implementor, Type target )
                : base( first, s, implementor, target )
            {
            }

            public override string TargetName => Target.FullName!;
        }

    }


}





