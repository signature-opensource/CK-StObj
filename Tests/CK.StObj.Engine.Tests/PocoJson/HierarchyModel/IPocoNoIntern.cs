using CK.Core;

namespace CK.StObj.Engine.Tests.PocoJson
{
    [ExternalName( "NoIntern" )]
    public interface IPocoNoIntern : IPoco
    {
        Person? Person { get; set; }
        Teacher? Teacher { get; set; }
        Student? Student { get; set; }
    }

}
