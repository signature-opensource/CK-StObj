using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Encapsulates Service result analysis. 
    /// </summary>
    public class AutoServiceCollectorResult
    {
        internal AutoServiceCollectorResult(
            bool success,
            IReadOnlyList<AutoServiceInterfaceInfo> allInterfaces,
            IReadOnlyList<AutoServiceInterfaceInfo> leafInterfaces,
            IReadOnlyList<AutoServiceInterfaceInfo> rootInterfaces,
            IReadOnlyList<AutoServiceClassInfo> rootClasses,
            IReadOnlyList<IReadOnlyList<AutoServiceClassInfo>> classAmbiguities,
            IReadOnlyList<Type> abstractTails,
            IReadOnlyList<AutoServiceClassInfo> subGraphs )
        {
            AllInterfaces = allInterfaces;
            LeafInterfaces = leafInterfaces;
            RootInterfaces = rootInterfaces;
            RootClasses = rootClasses;
            ClassAmbiguities = classAmbiguities ?? Array.Empty<IReadOnlyList<AutoServiceClassInfo>>();
            AbstractTails = abstractTails ?? Array.Empty<Type>();
            SubGraphRootClasses = subGraphs;
            // 
            HasFatalError = !success || ClassAmbiguities.Count > 0;
        }

        /// <summary>
        /// Gets the most specialized service interfaces found.
        /// Extended base interfaces are not exposed here.
        /// Use <see cref="AutoServiceInterfaceInfo.Interfaces"/> to retrieve the base interfaces.
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo> LeafInterfaces { get; }

        /// <summary>
        /// Gets the primary service interfaces found, the ones that don't extend any
        /// other service interfaces.
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo> RootInterfaces { get; }

        /// <summary>
        /// Gets the all the service interfaces found.
        /// </summary>
        public IReadOnlyList<AutoServiceInterfaceInfo> AllInterfaces { get; }

        /// <summary>
        /// Gets the root service implementations found.
        /// Specializations are not exposed here.
        /// Use <see cref="AutoServiceClassInfo.MostSpecialized"/> that is necessarily not null
        /// if no error occured to obtain the final class to use and <see cref="AutoServiceClassInfo.Specializations"/>
        /// to discover sub graphs of mapped classes.
        /// </summary>
        public IReadOnlyList<AutoServiceClassInfo> RootClasses { get; }

        /// <summary>
        /// Gets all the class ambiguities: the first item of each list is the ambiguity, following
        /// items are the most specialized leaves among which no unifiers have been found.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<AutoServiceClassInfo>> ClassAmbiguities { get; }

        /// Gets the list of tails that are abstract types.
        /// Abstract tails are ignored. <see cref="LogErrorAndWarnings(IActivityMonitor)"/> emits
        /// a warning for them.
        public IReadOnlyList<Type> AbstractTails { get; }

        /// <summary>
        /// Gets the subgraphs classes. It is either an independent leaf (its own most specialized) or
        /// a base class that has a MostSpecialized that differ from the one of its generalization.
        /// </summary>
        public IReadOnlyList<AutoServiceClassInfo> SubGraphRootClasses { get; }

        /// <summary>
        /// Gets whether an error exists that prevents the process to continue.
        /// </summary>
        /// <returns>
        /// False to continue the process (only warnings - or error considered as 
        /// warning - occured), true to stop remaining processes.
        /// </returns>
        public bool HasFatalError { get; }

        /// <summary>
        /// Logs detailed information about discovered items.
        /// </summary>
        /// <param name="monitor">Logger (must not be null).</param>
        public void LogErrorAndWarnings( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor" );
            using( monitor.OpenTrace( $"Auto Services: {LeafInterfaces.Count} most specialized interfaces and {RootClasses.Count} concrete paths." ) )
            {
                foreach( var a in ClassAmbiguities )
                {
                    monitor.Error( $"Base class '{a[0].Type.FullName}' cannot be unified by any of this candidates: '{a.Skip( 1 ).Select( t => t.Type.FullName ).Concatenate( "', '" )}'." );
                }
                RealObjectCollectorResult.CommonLogAndWarings( monitor, AbstractTails );
            }
        }

    }
}
