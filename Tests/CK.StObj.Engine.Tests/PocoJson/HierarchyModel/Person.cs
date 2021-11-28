using CK.Core;
using System;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName("CP")]
    public class Person
    {
        public Person( string name )
        {
            if( name.Contains( '|', StringComparison.Ordinal ) ) throw new ArgumentException( "Invalid | in name.", nameof( Name ) );
            Name = name;
        }

        public string Name { get; }

        public static Person Parse( string s ) => new Person( s );

        public override string ToString() => Name;

    }

}
