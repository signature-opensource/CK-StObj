using System;
using System.Collections;
using System.Security.AccessControl;

namespace CK.Setup
{
    /// <summary>
    /// This service exposes the <see cref="IPocoTypeSystem"/> and
    /// monitors whether new types appear across the generation steps:
    /// when no new types are registered after a generation step, <see cref="IsLocked"/>
    /// event is raised.
    /// <para>
    /// We must give a chance to other generators to register new types (typically the result of Cris ICommand&lt;TResult&gt;).
    /// We have to wait for all the PocoType to be registered, but how do we know that other generators are done with all
    /// their required registrations?
    /// Instead of doing one hop on the trampoline and prey that no one will need more, we continue hopping until no
    /// more new registrations are done.
    /// </para>
    /// <para>
    /// The <see cref="IsLocked"/> event should be replaced with [WaitFor] attributes on injected parameter services
    /// and, instead of being immediately injected in the current run service container, this
    /// service should be added when the type system is locked.
    /// Instead of relying on events like this one, this [WaitFor] would be easy to use and easily
    /// allow multiple wait conditions to be satisfied.
    /// (After one generation step where no pending implementors can be called, an error will be triggered.)
    /// </para>
    /// </summary>
    public interface ILockedPocoTypeSystem
    {
        /// <summary>
        /// Gets the type system that may be <see cref="IPocoTypeSystem.IsLocked"/> or not.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Raised once the <see cref="TypeSystem"/> has been locked because no
        /// new types have been registered during the previous generation step.
        /// </summary>
        event Action IsLocked;
    }

}
