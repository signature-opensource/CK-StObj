using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Core
{
    /// <summary>
    /// Decorates an interface, a class or an enum with its name and optional previous names.
    /// Without this attribute, the type name is the <see cref="Type.FullName"/>.
    /// For Poco interfaces, the primary interface that defines the Poco sets the Poco's name: it must
    /// be set on the primary interface.
    /// </summary>
    [AttributeUsage( AttributeTargets.Interface|AttributeTargets.Class|AttributeTargets.Enum, AllowMultiple = false, Inherited = false )]
    public class ExternalNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="ExternalNameAttribute"/> with the current name and a set of optional previous names.
        /// </summary>
        /// <param name="name">The current name. Must not be null, empty or starts with a '!'.</param>
        /// <param name="previousNames">Any number of previous names.</param>
        public ExternalNameAttribute( string name, params string[] previousNames )
        {
            CheckName( name );
            foreach( var n in previousNames ) CheckName( n );
            if( previousNames.Contains( name ) || previousNames.GroupBy( Util.FuncIdentity ).Count() > 1 )
            {
                throw new ArgumentException( "Duplicate names in attribute.", nameof( previousNames ) );
            }
            CommandName = name;
            PreviousNames = previousNames;
        }

        static void CheckName( string name )
        {
            if( String.IsNullOrWhiteSpace( name ) )
            {
                if( name == null ) throw new ArgumentNullException( nameof( name ) );
                throw new ArgumentException( $"Poco name must not be empty or whitespace.", nameof( name ) );
            }
            if( name.StartsWith( '!' ) )
            {
                throw new ArgumentException( $"Poco name must not start with a '!': '{name}'.", nameof( name ) );
            }
        }

        /// <summary>
        /// Gets the Poco name.
        /// The name must not start with a '!'.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Gets the previous names.
        /// </summary>
        public IReadOnlyCollection<string> PreviousNames { get; }
    }
}
