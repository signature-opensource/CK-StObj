namespace CK.Core
{
    /// <summary>
    /// This interface marker states that a class or an interface instance
    /// is a front process service: any service that depends on it must also be a
    /// process service.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IFrontProcessAutoService" defined in any namespace will be considered as
    /// a valid marker, regardless of the fact that it specializes any interface
    /// named "IAutoService".
    /// </para>
    /// </summary>
    public interface IFrontProcessAutoService : IAutoService
    {
    }

}
