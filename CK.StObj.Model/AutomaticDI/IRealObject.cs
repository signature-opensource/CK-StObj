namespace CK.Core
{
    /// <summary>
    /// This interface marker states that a class or an interface instance
    /// is a gateway to an external resource.
    /// Such gateways are necessarily unique instance in a "context" that is defined
    /// by the <see cref="IStObjMap"/>.
    /// <para>
    /// It is not required to be this exact type: any empty interface (no members)
    /// named "IRealObject" defined in any namespace will be considered as
    /// a valid marker (this duck typing is the same as <see cref="IAutoService"/> markers).
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// If the lifetimes of this IRealObject and <see cref="ISingletonAutoService"/>
    /// instances are the same, their roles are different as well as the way they are handled.
    /// Real objects can not use any constructor injection and support StObjConstruct/StObjInitialize
    /// private methods that isolate dependencies between a base class and its specializations whereas
    /// singleton services relies on "normal" constructor injection.
    /// </para>
    /// <para>
    /// Note that a class that is a IRealObject can perfectly implement a IAutoService: this will be
    /// resolved as a singleton (a ISingletonAutoService) and will be a potential implementation of the service
    /// (that may be replaced). This applies only to class, not to interface: an interface cannot be both
    /// a IAutoService and a IRealObject.
    /// </para>
    /// <para>
    /// Real objects must be used to model actual singletons "in real life", typically external resources
    /// with wich the system interacts such as a data repository or an external service.
    /// Singleton services are more classical services that happens to be able to be shared by different
    /// activities because of their thread safety and the fact that they depend only on other singleton
    /// services or real objects.
    /// </para>
    /// </remarks>
    public interface IRealObject
    {
    }

}
