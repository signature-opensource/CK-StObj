using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Engine.TypeCollector
{
    public sealed class TypeCollector
    {
        readonly Dictionary<Type,CachedType> _types;
        readonly AssemblyCollector _assemblies;

        TypeCollector( AssemblyCollector assemblies, Dictionary<Type, CachedType> types )
        {
            _types = types;
            _assemblies = assemblies;
        }

        internal static TypeCollector Create( IActivityMonitor monitor, AssemblyCollector assemblies, Dictionary<Type, CachedAssembly?> types )
        {
            var cache = new Dictionary<Type, CachedType>();
            foreach( var (type, assembly) in types )
            {
                var a = assembly ?? assemblies.EnsureAssembly( type.Assembly );
                var t = new CachedType( type, a );
                cache.Add( type, t );
            }
            return new TypeCollector( assemblies, cache );
        }

        /// <summary>
        /// Registers a type that must be a public enum, value type, class or interface. 
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="type">The type.</param>
        /// <returns>The cached type on success, null on error.</returns>
        public ICachedType? Register( IActivityMonitor monitor, Type type )
        {
            ThrowOnInvalidType( type, allowGenericTypedefinition: true );
            return DoRegister( monitor, type );
        }

        /// <summary>
        /// Registers a type that must be must be a public enum, value type, class, interface or generic type definition.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="type">The type.</param>
        /// <returns>The cached type on success, null on error.</returns>
        public ICachedType? Register( IActivityMonitor monitor, Type type, AutoServiceKind kind )
        {
            ThrowOnInvalidType( type, allowGenericTypedefinition: true );
            // Type detector is not yet integrated.
            return DoRegister( monitor, type );
        }

        ICachedType? DoRegister( IActivityMonitor monitor, Type type )
        {
            if( _types.TryGetValue( type, out var t ) )
            {
                t = new CachedType( type, _assemblies.EnsureAssembly( type.Assembly ) );
                _types.Add( type, t );
            }
            return t;
        }

        static void ThrowOnInvalidType( Type type, bool allowGenericTypedefinition )
        {
            Throw.CheckNotNullArgument( type );
            var invalid = GetTypeInvalidity( type, allowGenericTypedefinition );
            if( invalid != null ) Throw.CKException( $"Invalid type: '{type:N}' {invalid}." );
        }

        internal static string? GetTypeInvalidity( Type type, bool allowGenericTypedefinition )
        {
            if( type.FullName == null )
            {
                // Type.FullName is null if the current instance represents a generic type parameter, an array
                // type, pointer type, or byref type based on a type parameter, or a generic type
                // that is not a generic type definition but contains unresolved type parameters.
                // This FullName is also null for (at least) classes nested into nested generic classes.
                // In all cases, we don't handle it.
                return "has a null FullName";
            }
            if( type.Assembly.IsDynamic )
            {
                return "is defined by a dynamic assembly";
            }
            if( !type.IsVisible )
            {
                return "must be public (visible outside of its asssembly)";
            }
            if( type.IsClass || type.IsEnum || type.IsValueType || (allowGenericTypedefinition && type.IsGenericTypeDefinition) )
            {
                return null;
            }
            if( allowGenericTypedefinition )
            {
                return "must be an enum, a value type, a class, an interface or a generic type definition";
            }
            return "must be an enum, a value type, a class or an interface";
        }

        internal static bool IsValidType( IActivityMonitor monitor, Type type, bool allowGenericTypedefinition )
        {
            var invalid = GetTypeInvalidity( type, allowGenericTypedefinition );
            if( invalid != null )
            {
                monitor.Error( $"Invalid type registration '{type:N}' {invalid}." );
                return false;
            }
            return true;
        }

    }
}
