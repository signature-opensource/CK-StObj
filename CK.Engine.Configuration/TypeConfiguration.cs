using CK.Core;
using System;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Models the &lt;Type&gt; elements that are children of <see cref="TypeConfigurationSet"/> &lt;Types&gt;.
    /// </summary>
    /// <param name="Type">The type</param>
    /// <param name="Kind">The type kind. <see cref="ConfigurableAutoServiceKind.None"/> only ensures that the type is registered.</param>
    public sealed record TypeConfiguration( Type Type, ConfigurableAutoServiceKind Kind = ConfigurableAutoServiceKind.None )
    {
        internal TypeConfiguration( XElement e )
            : this( ReadType( e ), ReadKind( e ) )
        {
            if( e.Attribute( EngineConfiguration.xOptional ) != null )
            {
                Throw.InvalidDataException( "Obsolete Optional attribute. Please remove it." );
            }
            Type = SimpleTypeFinder.WeakResolver( (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value, throwOnError: true )!;
        }

        static Type ReadType( XElement e )
        {
            return SimpleTypeFinder.WeakResolver( (string?)e.Attribute( EngineConfiguration.xName ) ?? e.Value, throwOnError: true )!;
        }

        static ConfigurableAutoServiceKind ReadKind( XElement e )
        {
            var k = (string?)e.Attribute( EngineConfiguration.xKind );
            return k != null ? (ConfigurableAutoServiceKind)Enum.Parse( typeof( ConfigurableAutoServiceKind ), k.Replace( '|', ',' ) ) : ConfigurableAutoServiceKind.None;
        }

        /// <summary>
        /// Centralized helper: checks null type FullName, dynamic assembly, non visible type, attempt to set a non None kind on a
        /// IAutoService, IRealObject or IPoco and allows only enums, classes, interfaces or value types. 
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <param name="kind">The kind to check.</param>
        /// <returns>A non null error message to be included in a longer one $"{type.Name} {error}" or null if there is no error.</returns>
        public static string? GetConfiguredTypeErrorMessage( Type type, ConfigurableAutoServiceKind kind )
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
            if( kind != ConfigurableAutoServiceKind.None )
            {
                string? k = null;
                if( typeof( IAutoService ).IsAssignableFrom( type ) )
                {
                    k = nameof( IAutoService );
                }
                else if( typeof( IRealObject ).IsAssignableFrom( type ) )
                {
                    k = nameof( IRealObject );
                }
                else if( typeof( IPoco ).IsAssignableFrom( type ) )
                {
                    k = nameof( IPoco );
                }
                if( k != null )
                {
                    return $"is a {k}. IAutoService, IRealObject and IPoco cannot be externally configured";
                }
            }
            if( type.IsClass || type.IsEnum || type.IsValueType || type.IsInterface )
            {
                return null;
            }
            return "must be an enum, a value type, a class or an interface";
        }


    }
}
