using CK.Core;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName( "CS:CP" )]
    public class Student : Person
    {
        public Student( string name, int grade )
            : base( name )
        {
            Grade = grade;
        }

        public int Grade { get; set; }

        public new static Student Parse( string s )
        {
            var p = s.Split( "|" );
            return new Student( p[0], int.Parse( p[1] ) );
        }

        public override string ToString() => $"{Name}|{Grade}";
    }

}
