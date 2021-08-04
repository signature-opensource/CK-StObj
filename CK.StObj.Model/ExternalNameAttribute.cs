using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Core
{
    /// <summary>
    /// Decorates an interface, a class or an enum with its name and optional previous names.
    /// <para>
    /// Without this attribute, the type name is the <see cref="Type.FullName"/>, an external name must "look like" a
    /// .Net full type name (optional namespace and type name). Open generics should expose their generic parameter
    /// type names between parentheses - like <c>MyClass(T)</c> or <c>BiList(T1,T2)</c> - rather than just the 'count.
    /// Using parentheses rather than angle brackets like <c>BiList&lt;T1,T2&gt;</c> makes this name "more easily exportable".
    /// For example, in JSON, &lt;&gt; are - by default - encoded as "\u003C\u003E". And  &lt; or &gt; are forbidden in
    /// file names.
    /// </para>
    /// <para>
    /// For Poco interfaces, the primary interface that defines the Poco settles the Poco's type name: it must
    /// be set on the primary interface.
    /// </para>
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
            Name = name;
            PreviousNames = previousNames;
        }

        static void CheckName( string name )
        {
            if( String.IsNullOrWhiteSpace( name ) )
            {
                if( name == null ) throw new ArgumentNullException( nameof( name ) );
                throw new ArgumentException( $"External name must not be empty or whitespace.", nameof( name ) );
            }
            if( name.StartsWith( '!' ) )
            {
                throw new ArgumentException( $"External name must not start with a '!': '{name}'.", nameof( name ) );
            }
        }

        /// <summary>
        /// Gets the external name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the previous names.
        /// </summary>
        public IReadOnlyCollection<string> PreviousNames { get; }
    }
}
