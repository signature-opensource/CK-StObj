using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CSemVer;
using CK.Core;
using System.Collections.Immutable;
using System.Collections;

namespace CK.Setup
{
    /// <summary>
    /// Internal mutable implementation of <see cref="IStObjObjectEngineMap"/> that handles <see cref="MutableItem"/>.
    /// The internal participants have write access to it. I'm not proud of this (this is a mess) but it works...
    /// The map is instantiated by CKTypeCollector.GetRealObjectResult and then
    /// then internally exposed by the RealObjectCollectorResult so that CKTypeCollector.GetAutoServiceResult(RealObjectCollectorResult)
    /// can use (and fill) it.
    /// </summary>
    sealed partial class StObjObjectEngineMap : IStObjEngineMap, IStObjObjectEngineMap, IStObjServiceEngineMap
    {
        readonly Dictionary<object, MutableItem> _map;
        readonly IReadOnlyList<MutableItem> _finaImplementations;
        readonly IReadOnlyDictionary<Assembly,bool> _assemblies;

        // Ultimate result: StObjCollector.GetResult sets this if no error occurred
        // during Real objects processing.
        // This is awful.
        // This is ugly.
        // This sucks...
        IReadOnlyList<MutableItem>? _orderedStObjs;
        Dictionary<Type, ITypeAttributesCache>? _allTypesAttributesCache;
        IEndpointResult? _endpointResult;
        IReadOnlyDictionary<Type, IStObjMultipleInterface>? _multiplemappings;

        /// <summary>
        /// Initializes a new <see cref="StObjObjectEngineMap"/>.
        /// </summary>
        /// <param name="names">The final map names.</param>
        /// <param name="allSpecializations">
        /// Pre-dimensioned array that will be filled with actual
        /// mutable items by <see cref="StObjCollector.GetResult(IActivityMonitor)"/>.
        /// </param>
        /// <param name="assemblies">Reference to the set of assemblies used to implement the IStObjMap.Features property.</param>
        internal StObjObjectEngineMap( IReadOnlyList<string> names,
                                       IReadOnlyList<MutableItem> allSpecializations,
                                       IReadOnlyDictionary<Assembly,bool> assemblies )
        {
            Debug.Assert( names != null );
            Names = names;
            _map = new Dictionary<object, MutableItem>();
            _finaImplementations = allSpecializations;
            _assemblies = assemblies;

            _serviceSimpleMap = new Dictionary<Type, IStObjServiceFinalSimpleMapping>();
            _serviceSimpleList = new List<IStObjServiceFinalSimpleMapping>();
            _exposedServiceMap = _serviceSimpleMap.AsIReadOnlyDictionary<Type, IStObjServiceFinalSimpleMapping, IStObjServiceClassDescriptor>();

            _serviceToObjectMap = new Dictionary<Type, MutableItem>();
            _serviceRealObjects = new List<MutableItem>();
            _serviceToObjectMapExposed = _serviceToObjectMap.AsIReadOnlyDictionary<Type,MutableItem,IStObjFinalImplementation>();
        }

        internal void AddClassMapping( Type t, MutableItem m )
        {
            Debug.Assert( t.IsClass );
            _map.Add( t, m );
            if( t != m.RealObjectType.Type ) m.RealObjectType.AddUniqueMapping( t );
        }

        internal void AddInterfaceMapping( Type t, MutableItem m, MutableItem finalType )
        {
            Debug.Assert( t.IsInterface );
            _map.Add( t, finalType );
            _map.Add( new RealObjectInterfaceKey( t ), m );
            finalType.RealObjectType.AddUniqueMapping( t );
        }

        /// <summary>
        /// This auto implements the <see cref="IStObjObjectEngineMap"/>.
        /// </summary>
        public IStObjObjectEngineMap StObjs => this;

        SHA1Value IStObjMap.GeneratedSignature => SHA1Value.Empty;

        IStObjObjectMap IStObjMap.StObjs => this;

        /// <summary>
        /// Gets the map names.
        /// </summary>
        public IReadOnlyList<string> Names { get; }

        public IStObjFinalClass? ToLeaf( Type t ) => _map.GetValueOrDefault( t )
                                                     ?? (IStObjFinalClass?)_serviceSimpleMap.GetValueOrDefault( t )
                                                     ?? _serviceToObjectMap.GetValueOrDefault( t );

        /// <summary>
        /// Gets all the specialization. If there is no error, this list corresponds to the
        /// last items of the <see cref="RealObjectCollectorResult.ConcreteClasses"/>.
        /// </summary>
        internal IReadOnlyCollection<MutableItem> FinalImplementations => _finaImplementations;
        
        /// <summary>
        /// Gets all the mappings from object (including <see cref="RealObjectInterfaceKey"/>) to
        /// <see cref="MutableItem"/>.
        /// </summary>
        internal IReadOnlyDictionary<object, MutableItem> RawMappings => _map;

        /// <summary>
        /// Gets the most "abstract" item for a type.
        /// </summary>
        /// <param name="t">Any mapped type.</param>
        /// <returns>The most abstract, less specialized, associated StObj.</returns>
        internal MutableItem? ToHighestImpl( Type t )
        {
            Throw.CheckNotNullArgument( t );
            if( _map.TryGetValue( t, out MutableItem? c ) )
            {
                if( c.RealObjectType.Type != t )
                {
                    if( t.IsInterface )
                    {
                        _map.TryGetValue( new RealObjectInterfaceKey( t ), out c );
                    }
                    else
                    {
                        while( (c = c.Generalization) != null )
                        {
                            if( c.RealObjectType.Type == t ) break;
                        }
                    }
                }
            }
            return c;
        }

        IStObjResult? IStObjObjectEngineMap.ToHead( Type t ) => ToHighestImpl( t );

        object? IStObjObjectMap.Obtain( Type t ) => _map.GetValueOrDefault( t )?.InitialObject;

        IReadOnlyList<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finaImplementations;

        IReadOnlyCollection<IStObjFinalImplementationResult> IStObjObjectEngineMap.FinalImplementations => _finaImplementations;

        IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _map.Where( kv => kv.Key is Type ).Select( kv => new StObjMapping( kv.Value, kv.Value.FinalImplementation ) );

        IStObjFinalImplementationResult? IStObjObjectEngineMap.ToLeaf( Type t ) => _map.GetValueOrDefault( t );

        IReadOnlyList<IStObjResult> IStObjObjectEngineMap.OrderedStObjs => _orderedStObjs ?? Array.Empty<MutableItem>();

        IReadOnlyDictionary<Type, ITypeAttributesCache> IStObjEngineMap.AllTypesAttributesCache => (IReadOnlyDictionary<Type, ITypeAttributesCache>?)_allTypesAttributesCache ?? ImmutableDictionary<Type, ITypeAttributesCache>.Empty;

        IEndpointResult IStObjEngineMap.EndpointResult => _endpointResult!;

        IReadOnlyDictionary<Type, IStObjMultipleInterface> IStObjMap.MultipleMappings => _multiplemappings!;

        internal void SetFinalOrderedResults( IReadOnlyList<MutableItem> ordered,
                                              Dictionary<Type,ITypeAttributesCache> allTypesAttributesCache,
                                              IEndpointResult? endpointResult,
                                              IReadOnlyDictionary<Type, IStObjMultipleInterface> multipleMappings )
        {
            _orderedStObjs = ordered;
            _allTypesAttributesCache = allTypesAttributesCache;
            _endpointResult = endpointResult;
            _multiplemappings = multipleMappings;
        }

        IStObjFinalImplementation? IStObjObjectMap.ToLeaf( Type t ) => _map.GetValueOrDefault( t );

        bool IStObjMap.ConfigureServices( in StObjContextRoot.ServiceRegister register )
        {
            throw new NotSupportedException( "ConfigureServices is not supported at build time." );
        }

        /// <summary>
        /// Dynamically projects <see cref="CKTypeCollectorResult.Assemblies"/> to their <see cref="VFeature"/>
        /// (ordered by <see cref="VFeature.Name"/> since by design there cannot be multiple versions by feature).
        /// </summary>
        public IReadOnlyCollection<VFeature> Features => _assemblies.Where( kv => kv.Value )
                                                                    .Select( kv => ToVFeature( kv.Key ) )
                                                                    .OrderBy( Util.FuncIdentity )
                                                                    .ToList();

        static VFeature ToVFeature( Assembly a )
        {
            Debug.Assert( a != null );
            Debug.Assert( a.GetName().Name != null );
            var v = InformationalVersion.ReadFromAssembly( a ).Version;
            return new VFeature( a.GetName().Name!, v != null && v.IsValid ? v : SVersion.ZeroVersion );
        }

    }
}
