using CK.Core;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName( "AllOfThem" )]
    public interface IPocoAllOfThem : IPoco
    {
        Intern? Intern { get; set; }
        Student? Student { get; set; }
        Teacher? Teacher { get; set; }
        Person? Person { get; set; }
    }

}
