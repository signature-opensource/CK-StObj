using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Result of the <see cref="CKTypeCollector"/> work.
    /// </summary>
    public class CKTypeCollectorResult
    {
        internal CKTypeCollectorResult(
            ISet<Assembly> assemblies,
            IPocoSupportResult pocoSupport,
            RealObjectCollectorResult c,
            AutoServiceCollectorResult s,
            CKTypeKindDetector typeKindDetector )
        {
            PocoSupport = pocoSupport;
            Assemblies = assemblies;
            RealObjects = c;
            AutoServices = s;
            TypeKindDetector = typeKindDetector;
        }

        /// <summary>
        /// Gets all the registered Poco information.
        /// </summary>
        public IPocoSupportResult PocoSupport { get; }

        /// <summary>
        /// Gets the set of asssemblies for which at least one type has been registered.
        /// </summary>
        public ISet<Assembly> Assemblies { get; }

        /// <summary>
        /// Gets the reults for <see cref="IRealObject"/> objects.
        /// </summary>
        public RealObjectCollectorResult RealObjects { get; }

        /// <summary>
        /// Gets the reults for <see cref="IScopedAutoService"/> objects.
        /// </summary>
        public AutoServiceCollectorResult AutoServices { get; }

        /// <summary>
        /// Gets the ambient type detector.
        /// </summary>
        public CKTypeKindDetector TypeKindDetector { get; }

        /// <summary>
        /// Gets whether an error exists that prevents the process to continue.
        /// </summary>
        /// <returns>
        /// False to continue the process (only warnings - or error considered as 
        /// warning - occured), true to stop remaining processes.
        /// </returns>
        public bool HasFatalError => PocoSupport == null || RealObjects.HasFatalError || AutoServices.HasFatalError;

        /// <summary>
        /// Gets all the <see cref="ImplementableTypeInfo"/>: Abstract types that require a code generation
        /// that are either <see cref="IAutoService"/>, <see cref="IRealObject"/> (or both).
        /// </summary>
        public IEnumerable<ImplementableTypeInfo> TypesToImplement
        {
            get
            {
                var all = RealObjects.EngineMap.AllSpecializations.Select( m => m.ImplementableTypeInfo )
                            // Filters out the Service implementation that are RealObject.
                            .Concat( AutoServices.RootClasses.Select( c => c.MostSpecialized.IsRealObject ? null : c.MostSpecialized.ImplementableTypeInfo ) )
                            .Concat( AutoServices.SubGraphRootClasses.Select( c => c.MostSpecialized.IsRealObject ? null : c.MostSpecialized.ImplementableTypeInfo ) )
                            .Where( i => i != null );

                Debug.Assert( all.GroupBy( i => i ).Where( g => g.Count() > 1 ).Any() == false, "No duplicates." );
                return all;
            }
        }

        /// <summary>
        /// Logs detailed information about discovered items.
        /// </summary>
        /// <param name="monitor">Logger (must not be null).</param>
        public void LogErrorAndWarnings( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof(monitor) );
            using( monitor.OpenTrace( $"Collector summary:" ) )
            {
                if( PocoSupport == null )
                {
                    monitor.Fatal( $"Poco support failed!" );
                }
                RealObjects.LogErrorAndWarnings( monitor );
                AutoServices.LogErrorAndWarnings( monitor );
            }
        }

    }

}
