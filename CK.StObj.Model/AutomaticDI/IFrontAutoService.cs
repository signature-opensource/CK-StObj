namespace CK.Core
{
    /// <summary>
    /// This interface marker states that a class or an interface instance
    /// is a "front only" service: any service that depends on it must also be a
    /// "front only" service. Note that this "only" aspect is canceled if (and only if)
    /// the <see cref="IMarshallableAutoService"/> marker interface is also used.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IFrontAutoService" defined in any namespace will be considered as
    /// a valid marker, regardless of the fact that it specializes any interface
    /// named "IAutoService".
    /// </para>
    /// </summary>
    public interface IFrontAutoService : IAutoService
    {
    }

}
