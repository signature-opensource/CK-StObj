using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Context that is given to <see cref="IStObjEngineAspect.Run"/> method.
    /// </summary>
    public interface IStObjEngineRunContext
    {
        /// <summary>
        /// Gets engine status information.
        /// </summary>
        IStObjEngineStatus EngineStatus { get; }

        /// <summary>
        /// Gets the service container.
        /// Exposing the container itself during the run (instead of the mere <see cref="IServiceProvider"/>)
        /// enables aspects to dynamically register services while running.
        /// </summary>
        ISimpleServiceContainer ServiceContainer { get; }

        /// <summary>
        /// Gets the list of all available aspects.
        /// </summary>
        IReadOnlyList<IStObjEngineAspect> Aspects { get; }

        /// <summary>
        /// Gets all the <see cref="IStObjResult"/> ordered by their dependencies.
        /// </summary>
        IReadOnlyList<IStObjResult> OrderedStObjs { get; }

        /// <summary>
        /// Gets the features.
        /// </summary>
        IReadOnlyCollection<VFeature> Features { get; }

        /// <summary>
        /// Pushes a defered action.
        /// It will be executed after the Run call on all aspects.
        /// An action can be pushed at any moment and a pushed action can push another action.
        /// </summary>
        /// <param name="postAction">Action to execute.</param>
        void PushDeferredAction( Func<IActivityMonitor, IStObjEngineRunContext, bool> postAction );
    }

}
