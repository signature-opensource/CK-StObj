namespace CK.Core
{
    /// <summary>
    /// This interface marker states that all concrete classes that support it must  
    /// be automatically registered. When applied to an interface or an abstract base class,
    /// each mapping must be registered, regardless of any existing registrations.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IMultipleAutoService" defined in any namespace will be considered as
    /// a valid marker.
    /// </para>
    /// <para>
    /// A "Multiple Service" is not compatible with <see cref="IRealObject"/> and must be
    /// applied "before"/"above" any other auto services: if <see cref="IMultipleAutoService"/> is
    /// supported by an interface or a class that is already marked with another marker, an error
    /// is raised.
    /// </para>
    /// </summary>
    public interface IMultipleAutoService : IAutoService
    {
    }

}
