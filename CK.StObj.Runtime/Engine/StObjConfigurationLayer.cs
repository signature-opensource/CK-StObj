using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Template class that implements a Chain of Responsibility pattern on the different hooks called
    /// during the StObj build phasis (except the <see cref="IStObjRuntimeBuilder"/> methods).
    /// These configuration layers must be added to a <see cref="StObjEngineConfigurator"/>.
    /// It does nothing at its level except calling the <see cref="Next"/> configurator if it is not null.
    /// Methods are defined here in the order where they are called.
    /// </summary>
    public class StObjConfigurationLayer : IStObjTypeFilter, IStObjStructuralConfigurator, IStObjValueResolver
    {
        StObjConfigurationLayer _next;
        StObjEngineConfigurator _host;

        /// <summary>
        /// Gets the next <see cref="StObjConfigurationLayer"/> that should be called by all hooks in this configurator.
        /// Can be null.
        /// </summary>
        public StObjConfigurationLayer Next
        {
            get { return _next; }
            internal set { _next = value; }
        }

        /// <summary>
        /// Gets the configuration host to which this configurator has been added.
        /// Null if this configurator is not bound to a <see cref="StObjEngineConfigurator"/>.
        /// </summary>
        public StObjEngineConfigurator Host
        {
            get { return _host; }
            internal set { _host = value; }
        }

        /// <summary>
        /// Step n°1 - Types that participates to setup can be filtered.
        /// This empty implementation of <see cref="IStObjTypeFilter.TypeFilter"/> calls <see cref="Next"/> if it
        /// is not null otherwise it returns true to keep all types.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="t">Type to accept ot not.</param>
        /// <returns>True to keep the type, false to exclude it.</returns>
        public virtual bool TypeFilter( IActivityMonitor monitor, Type t )
        {
            return _next != null ? _next.TypeFilter( monitor, t ) : true;
        }

        /// <summary>
        /// Step n°2 - Once specialized objects are created, the configuration for each "slice" (StObj) from top to bottom of the inheritance chain 
        /// can be altered: properties can be set, dependencies like Container, Requires, Children, etc. but also parameters' value of the StObjConstruct method can be changed.
        /// This empty implementation of <see cref="IStObjStructuralConfigurator.Configure"/> calls <see cref="Next"/> if it is not null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="o">The item to configure.</param>
        public virtual void Configure( IActivityMonitor monitor, IStObjMutableItem o )
        {
            if( _next != null ) _next.Configure( monitor, o );
        }

        /// <summary>
        /// Step n°3 - Last step before ordering. Ambient properties that had not been resolved can be set to a value here.
        /// This empty implementation of <see cref="IStObjValueResolver.ResolveExternalPropertyValue"/> calls <see cref="Next"/> if it is not null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ambientProperty">Property for which a value should be set.</param>
        public virtual void ResolveExternalPropertyValue( IActivityMonitor monitor, IStObjFinalAmbientProperty ambientProperty )
        {
            if( _next != null ) _next.ResolveExternalPropertyValue( monitor, ambientProperty );
        }

        /// <summary>
        /// Step n°4 - StObj dependency graph has been ordered, properties that was settable before initialization
        /// have been set, the StObjConstruct method is called and for each of their parameters, this method enables
        /// the parameter value to be set or changed.
        /// This is the last step of the pure StObj level work: after this one, object graph dependencies have been resolved, objects are configured.
        /// This empty implementation of <see cref="IStObjValueResolver.ResolveParameterValue"/> calls <see cref="Next"/> if it is not null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="parameter">Parameter of a StObjConstruct method.</param>
        public virtual void ResolveParameterValue( IActivityMonitor monitor, IStObjFinalParameter parameter )
        {
            if( _next != null ) _next.ResolveParameterValue( monitor, parameter );
        }
    }


}
