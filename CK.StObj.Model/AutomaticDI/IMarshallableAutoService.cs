namespace CK.Core
{
    /// <summary>
    /// This interface marker states that a class or an interface instance
    /// is a front service (at least a <see cref="IFrontProcessAutoService"/> and maybe
    /// a <see cref="IFrontAutoService"/>) but a marshallable one: the service is no more
    /// "front only" since it can be mashalled.
    /// Note that a <see cref="CK.StObj.Model.IMarshaller{T}"/> must be available.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IMarshallableAutoService" defined in any namespace will be considered as
    /// a valid marker, regardless of the fact that it specializes any interface
    /// named "IFrontProcessAutoService".
    /// </para>
    /// </summary>
    public interface IMarshallableAutoService : IFrontProcessAutoService
    {
    }

}
