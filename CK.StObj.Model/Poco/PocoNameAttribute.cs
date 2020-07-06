using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Core
{
    /// <summary>
    /// Decorates a <see cref="IPoco"/> interface with its name and optional previous names.
    /// Without this attribute, the poco name is the <see cref="Type.FullName"/> of the primary
    /// interface that defines the Poco.
    /// </summary>
    [AttributeUsage( AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public class PocoNameAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="PocoNameAttribute"/> with the current name and a set of optional previous names.
        /// </summary>
        /// <param name="name">The current name. Must not be null, empty or starts with a '!'.</param>
        /// <param name="previousNames">Any number of previous names.</param>
        public PocoNameAttribute( string name, params string[] previousNames )
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
