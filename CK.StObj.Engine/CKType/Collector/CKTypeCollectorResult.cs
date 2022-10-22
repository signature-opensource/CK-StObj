using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CK.Core;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Result of the <see cref="CKTypeCollector"/> work.
    /// </summary>
    public class CKTypeCollectorResult
    {
        readonly IReadOnlyDictionary<Type, TypeAttributesCache?> _regularTypes;

        internal CKTypeCollectorResult( ISet<Assembly> assemblies,
                                        IPocoDirectory pocoDirectory,
                                        PocoTypeSystem pocoTypeSystem,
                                        RealObjectCollectorResult c,
                                        AutoServiceCollectorResult s,
                                        IReadOnlyDictionary<Type, TypeAttributesCache?> regularTypes,
                                        IAutoServiceKindComputeFacade kindComputeFacade )
        {
            PocoDirectory = pocoDirectory;
            PocoTypeSystem = pocoTypeSystem;
            Assemblies = assemblies;
            RealObjects = c;
            AutoServices = s;
            _regularTypes = regularTypes;
            KindComputeFacade = kindComputeFacade;
        }

        /// <summary>
        /// Gets all the registered Poco information.
        /// </summary>
        public IPocoDirectory PocoDirectory { get; }

        /// <summary>
        /// Gets the Poco Type system.
        /// </summary>
        public IPocoTypeSystem PocoTypeSystem { get; }

        /// <summary>
        /// Gets the set of assemblies for which at least one type has been registered.
        /// </summary>
        public ISet<Assembly> Assemblies { get; }

        /// <summary>
        /// Gets the results for <see cref="IRealObject"/> objects.
        /// </summary>
        public RealObjectCollectorResult RealObjects { get; }

        /// <summary>
        /// Gets the results for <see cref="IAutoService"/> objects.
        /// </summary>
        public AutoServiceCollectorResult AutoServices { get; }

        /// <summary>
        /// Gets the AutoServiceKind compute façade.
        /// </summary>
        internal IAutoServiceKindComputeFacade KindComputeFacade { get; }

        /// <summary>
        /// Gets whether an error exists that prevents the process to continue.
        /// Note that errors or fatals that may have been emitted while registering types
        /// are ignored here. The <see cref="StObjCollector"/> wraps all its work, including type registration
        /// in a <see cref="ActivityMonitorExtension.OnError(IActivityMonitor, Action)"/> block and consider
        /// any <see cref="LogLevel.Error"/> or <see cref="LogLevel.Fatal"/> to be fatal errors, but at this level,
        /// those are ignored.
        /// </summary>
        /// <returns>
        /// False to continue the process (only warnings - or error considered as 
        /// warning - occurred), true to stop remaining processes.
        /// </returns>
        public bool HasFatalError => PocoDirectory == null || RealObjects.HasFatalError || AutoServices.HasFatalError;

        /// <summary>
        /// Gets all the <see cref="ImplementableTypeInfo"/>: Abstract types that require a code generation
        /// that are either <see cref="IAutoService"/>, <see cref="IRealObject"/> (or both).
        /// </summary>
        public IEnumerable<ImplementableTypeInfo> TypesToImplement
        {
            get
            {
                var all = RealObjects.EngineMap.FinalImplementations.Select( m => m.ImplementableTypeInfo )
                            // Filters out the Service implementation that are RealObject.
                            .Concat( AutoServices.RootClasses.Select( c => c.IsRealObject ? null : c.MostSpecialized!.ImplementableTypeInfo ) )
                            .Concat( AutoServices.SubGraphRootClasses.Select( c => c.IsRealObject ? null : c.MostSpecialized!.ImplementableTypeInfo ) )
                            .Where( i => i != null )
                            .Select( i => i! );

                Debug.Assert( all.GroupBy( Util.FuncIdentity ).Where( g => g.Count() > 1 ).Any() == false, "No duplicates." );
                return all;
            }
        }


        /// <summary>
        /// Crappy hook...
        /// </summary>
        internal void SetFinalOrderedResults( IReadOnlyList<MutableItem> ordered )
        {
            // Compute the indexed AllTypesAttributesCache.
            Debug.Assert( AutoServices.AllClasses.All( c => !c.TypeInfo.IsExcluded ) );
            Debug.Assert( AutoServices.AllClasses.All( c => c.TypeInfo.Attributes != null ) );

            var all = ordered.Select( o => o.Attributes )
                          .Concat( AutoServices.AllClasses.Where( c => !c.IsRealObject ).Select( c => c.TypeInfo.Attributes! ) )
                          .Concat( AutoServices.AllInterfaces.Select( i => i.Attributes ) )
                          .Concat( _regularTypes.Values.Where( a => a != null ).Select( a => a! ) );

            Debug.Assert( all.GroupBy( Util.FuncIdentity ).Where( g => g.Count() > 1 ).Any() == false, "No duplicates." );

            RealObjects.EngineMap.SetFinalOrderedResults( ordered, all.ToDictionary( c => c.Type ) );
        }

        /// <summary>
        /// Logs detailed information about discovered items.
        /// </summary>
        /// <param name="monitor">Logger (must not be null).</param>
        public void LogErrorAndWarnings( IActivityMonitor monitor )
        {
            Throw.CheckNotNullArgument( monitor );
            using( monitor.OpenTrace( $"Collector summary:" ) )
            {
                if( PocoDirectory == null ) monitor.Fatal( $"Poco support failed!" );
                RealObjects.LogErrorAndWarnings( monitor );
                AutoServices.LogErrorAndWarnings( monitor );
            }
        }

    }

}
