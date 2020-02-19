using System;
using System.Collections.Generic;
using System.Linq;
using CK.Text;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// One of the <see cref="CKTypeCollector"/> work's result: handles <see cref="IRealObject"/>
    /// types. This and <see cref="AutoServiceCollectorResult"/> are exposed by
    /// the <see cref="CKTypeCollectorResult"/>.
    /// </summary>
    public class RealObjectCollectorResult
    {
        IReadOnlyList<IReadOnlyList<MutableItem>> _concreteClassesPath;

        internal RealObjectCollectorResult(
            StObjObjectEngineMap mappings,
            IReadOnlyList<IReadOnlyList<MutableItem>> concreteClasses,
            IReadOnlyList<IReadOnlyList<Type>> classAmbiguities,
            IReadOnlyList<IReadOnlyList<Type>> interfaceAmbiguities,
            IReadOnlyList<Type> abstractTails )
        {
            EngineMap = mappings;
            _concreteClassesPath = concreteClasses;
            ClassAmbiguities = classAmbiguities;
            InterfaceAmbiguities = interfaceAmbiguities;
            AbstractTails = abstractTails;
        }

        /// <summary>
        /// Gets the internal mappings.
        /// </summary>
        internal StObjObjectEngineMap EngineMap { get; }

        /// <summary>
        /// Gets all the paths from <see cref="IRealObject"/> base classes to their most
        /// specialized concrete classes that this context contains.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<IStObjMutableItem>> ConcreteClasses => _concreteClassesPath;

        /// <summary>
        /// Gets all the class ambiguities: the first type of each list corresponds to more than
        /// one following concrete specializations.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Type>> ClassAmbiguities { get; }

        /// <summary>
        /// Gets all the interfaces ambiguities: the first type is an interface that is implemented
        /// by more than one following concrete classes.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<Type>> InterfaceAmbiguities { get; }

        /// <summary>
        /// Gets the list of tails that are abstract types.
        /// Abstract tails are ignored. <see cref="LogErrorAndWarnings(IActivityMonitor)"/> emits
        /// a warning for them.
        /// </summary>
        public IReadOnlyList<Type> AbstractTails { get; }

        /// <summary>
        /// Gets whether an error exists that prevents the process to continue.
        /// </summary>
        /// <returns>
        /// False to continue the process (only warnings - or error considered as 
        /// warning - occured), true to stop remaining processes.
        /// </returns>
        public bool HasFatalError => ClassAmbiguities.Count != 0 || InterfaceAmbiguities.Count != 0;

        /// <summary>
        /// Logs detailed information about discovered real objects.
        /// </summary>
        /// <param name="monitor">Logger (must not be null).</param>
        public void LogErrorAndWarnings( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( "monitor" );
            using( monitor.OpenTrace( $"Real Objects: {EngineMap.MappedTypeCount} mappings for {_concreteClassesPath.Count} concrete paths." ) )
            {
                foreach( var a in InterfaceAmbiguities )
                {
                    monitor.Error( $"Interface '{a[0].FullName}' is implemented by more than one concrete classes: {a.Skip( 1 ).Select( t => t.FullName ).Concatenate( "', '" )}." );
                }
                foreach( var a in ClassAmbiguities )
                {
                    monitor.Error( $"Base class '{a[0].FullName}' has more than one concrete specialization: '{a.Skip( 1 ).Select( t => t.FullName ).Concatenate( "', '" )}'." );
                }
                CommonLogAndWarings( monitor, AbstractTails );
            }
        }

        internal static void CommonLogAndWarings( IActivityMonitor monitor, IReadOnlyList<Type> abstractTails )
        {
            if( abstractTails.Count > 0 )
            {
                monitor.Warn( $"Abstract classes without specialization are ignored: {abstractTails.Select( t => t.FullName ).Concatenate()}." );
            }
        }
    }

}
