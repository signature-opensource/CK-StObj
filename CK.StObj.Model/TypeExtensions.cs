using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Extends Type with helper methods.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Gets the <see cref="ExternalNameAttribute"/> names or this <see cref="Type.FullName"/> (and
        /// emits a warning if the full name is used).
        /// </summary>
        /// <param name="t">This type.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The name to use to identify the type.</param>
        /// <param name="previousNames">Optional previous names.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool GetExternalNames( this Type t, IActivityMonitor monitor, out string name, out string[] previousNames )
        {
            var names = t.GetCustomAttributesData().Where( d => typeof( ExternalNameAttribute ).IsAssignableFrom( d.AttributeType ) ).FirstOrDefault();

            if( names != null )
            {
                var args = names.ConstructorArguments;
                name = (string)args[0].Value!;
                previousNames = ((IEnumerable<CustomAttributeTypedArgument>)args[1].Value!).Select( a => (string)a.Value! ).ToArray();
                if( String.IsNullOrWhiteSpace( name ) )
                {
                    monitor.Error( $"Empty name in ExternalName attribute on '{t.FullName}'." );
                    return false;
                }
                if( previousNames.Any( n => String.IsNullOrWhiteSpace( n ) ) )
                {
                    monitor.Error( $"Empty previous name in ExternalName attribute on '{t.FullName}'." );
                    return false;
                }
                if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Any( g => g.Count() > 1 ) )
                {
                    monitor.Error( $"Duplicate ExternalName in attribute on '{t.FullName}'." );
                    return false;
                }
            }
            else
            {
                name = t.FullName!;
                previousNames = Array.Empty<string>();
                monitor.Warn( $"Type '{name}' use its full name as its name since no [ExternalName] attribute is defined." );
            }
            return true;
        }


    }
}
