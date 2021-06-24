using CK.Core;
using System.Globalization;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName( "CI:CT" )]
    public class Intern : Teacher
    {
        public Intern( string name, string currentLevel, int? salary )
            : base( name, currentLevel )
        {
            Salary = salary;
        }

        public int? Salary { get; set; }

        public new static Intern Parse( string s )
        {
            var p = s.Split( "|" );
            return new Intern( p[0], p[1], p[2] == "null" ? (int?)null : int.Parse( p[2], NumberFormatInfo.InvariantInfo ) );
        }

        public override string ToString() => $"{Name}|{CurrentLevel}|{Salary?.ToString( NumberFormatInfo.InvariantInfo ) ?? "null"}";
    }



}
