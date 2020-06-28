using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IDynamicAssembly.Memory"/> or <see cref="ICodeGenerationContext.GlobalMemory"/> with helpers.
    /// </summary>
    public static class DictionaryMemoryExtension
    {
        /// <summary>
        /// Tries to get the typed object (that can be null).
        /// Use <see cref="AddCachedInstance"/> to register a nullable instance of the type.
        /// </summary>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="this">This memory.</param>
        /// <param name="value">The cached instance. Can be null.</param>
        /// <returns>True on success, false if the type has no registered instance.</returns>
        public static bool TryGetCachedInstance<T>( this IDictionary<object, object?> @this, out T? value ) where T : class
        {
            if( @this.TryGetValue( typeof( T ), out var obj ) )
            {
                value = (T?)obj;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Adds a typed instance or throws if it already exists.
        /// </summary>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="this">This memory.</param>
        /// <param name="value">The cached instance. Can be null.</param>
        public static void AddCachedInstance<T>( this IDictionary<object, object?> @this, T? value ) where T : class
        {
            @this.Add( typeof( T ), value );
        }

        /// <summary>
        /// Gets or creates a typed instance thanks to a factory function.
        /// </summary>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="this">This memory.</param>
        /// <param name="creator">The factory function. Can return a null that will be stored.</param>
        /// <returns>The type instance.</returns>
        public static T? GetCachedInstance<T>( this IDictionary<object, object?> @this, Func<T?> creator ) where T : class
        {
            if( !TryGetCachedInstance<T>( @this, out var result ) )
            {
                result = creator();
                @this.Add( typeof( T ), result );
            }
            return result;
        }

    }

}
