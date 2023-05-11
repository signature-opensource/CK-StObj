namespace CK.Core
{
    /// <summary>
    /// This interface marker states that a class or an interface instance
    /// is a "front end point" service: any service that depends on it must also be a
    /// "front end point" service.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IEndpointAutoService" defined in any namespace will be considered as
    /// a valid marker, regardless of the fact that it specializes any interface
    /// named "IFrontProcessAutoService".
    /// </para>
    /// </summary>
    public interface IEndpointAutoService : IFrontProcessAutoService
    {
    }

}
