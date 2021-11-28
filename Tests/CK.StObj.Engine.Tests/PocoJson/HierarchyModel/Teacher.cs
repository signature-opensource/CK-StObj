using CK.Core;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName( "CT:CP" )]
    public class Teacher : Person
    {
        public Teacher( string name, string currentLevel )
            : base( name )
        {
            CurrentLevel = currentLevel;
        }

        public string CurrentLevel { get; set; }

        public new static Teacher Parse( string s )
        {
            var p = s.Split( "|" );
            return new Teacher( p[0], p[1] );
        }

        public override string ToString() => $"{Name}|{CurrentLevel}";
    }

}
