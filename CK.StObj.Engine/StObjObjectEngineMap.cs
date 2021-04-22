using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CSemVer;
using CK.Core;
using System.Collections.Immutable;

namespace CK.Setup
{
    /// <summary>
    /// Internal mutable implementation of <see cref="IStObjObjectEngineMap"/> that handles <see cref="MutableItem"/>.
    /// The internal participants have write access to it. I'm not proud of this (there are definitely cleaner
    /// ways to organize this) but it works...
    /// The map is instantiated by CKTypeCollector.GetRealObjectResult and then
    /// then internally exposed by the RealObjectCollectorResult so that CKTypeCollector.GetAutoServiceResult(RealObjectCollectorResult)
    /// can use (and fill) it.
    /// </summary>
    partial class StObjObjectEngineMap : IStObjEngineMap, IStObjObjectEngineMap, IStObjServiceEngineMap
    {
        readonly CKTypeKindDetector _typeKindDetector;
        readonly Dictionary<object, MutableItem> _map;
        readonly IReadOnlyList<MutableItem> _finaImplementations;
        readonly IReadOnlyCollection<Assembly> _assemblies;

        // Ultimate result: StObjCollector.GetResult sets this if no error occurred
        // during Real objects processing.
        IReadOnlyList<MutableItem>? _orderedStObjs;
        Dictionary<Type, ITypeAttributesCache>? _allTypesAttributesCache;

        /// <summary>
        /// Initializes a new <see cref="StObjObjectEngineMap"/>.
        /// </summary>
        /// <param name="names">The final map names.</param>
        /// <param name="allSpecializations">
        /// Pre-dimensioned array that will be filled with actual
        /// mutable items by <see cref="StObjCollector.GetResult()"/>.
        /// </param>
        /// <param name="typeKindDetector">The type kind detector.</param>
        /// <param name="assemblies">Reference to the set of assemblies used to implement the IStObjMap.Features property.</param>
        internal protected StObjObjectEngineMap(
            IReadOnlyList<string> names,
            IReadOnlyList<MutableItem> allSpecializations,
            CKTypeKindDetector typeKindDetector,
            IReadOnlyCollection<Assembly> assemblies )
        {
            Debug.Assert( names != null );
            Names = names;
            _map = new Dictionary<object, MutableItem>();
            _finaImplementations = allSpecializations;
            _assemblies = assemblies;

            _serviceSimpleMap = new Dictionary<Type, IStObjServiceFinalSimpleMapping>();
            _serviceSimpleList = new List<IStObjServiceFinalSimpleMapping>();
            _exposedServiceMap = _serviceSimpleMap.AsCovariantReadOnly<Type, IStObjServiceFinalSimpleMapping, IStObjServiceClassDescriptor>();

            _serviceManualMap = new Dictionary<Type, IStObjServiceFinalManualMapping>();
            _serviceManualList = new List<IStObjServiceFinalManualMapping>();
            _exposedManualServiceMap =  _serviceManualMap.AsCovariantReadOnly<Type, IStObjServiceFinalManualMapping, IStObjServiceClassFactory>();

            _serviceToObjectMap = new Dictionary<Type, MutableItem>();
            _serviceRealObjects = new List<MutableItem>();
            _serviceToObjectMapExposed = _serviceToObjectMap.AsCovariantReadOnly<Type,MutableItem,IStObjFinalImplementation>();

            _typeKindDetector = typeKindDetector;
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

        SHA1Value IStObjMap.GeneratedSignature => SHA1Value.EmptySHA1;

        IStObjObjectMap IStObjMap.StObjs => this;

        /// <summary>
        /// Gets the map names.
        /// </summary>
        public IReadOnlyList<string> Names { get; }

        IStObjFinalClass? IStObjEngineMap.Find( Type t ) => _map.GetValueOrDefault( t )
                                                            ?? (IStObjFinalClass?)_serviceSimpleMap.GetValueOrDefault( t )
                                                            ?? (IStObjFinalClass?)_serviceToObjectMap.GetValueOrDefault( t )
                                                            ?? _serviceManualMap.GetValueOrDefault( t );

        /// <summary>
        /// Gets all the specialization. If there is no error, this list corresponds to the
        /// last items of the <see cref="RealObjectCollectorResult.ConcreteClasses"/>.
        /// </summary>
        internal IReadOnlyCollection<MutableItem> FinalImplementations => _finaImplementations;

        /// <summary>
        /// Gets all the mapping from object (including <see cref="RealObjectInterfaceKey"/>) to
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
            if( t == null ) throw new ArgumentNullException( "t" );
            MutableItem? c;
            if( _map.TryGetValue( t, out c ) )
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

        IEnumerable<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finaImplementations.Select( m => m.FinalImplementation );

        IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _map.Where( kv => kv.Key is Type ).Select( kv => new StObjMapping( kv.Value, kv.Value.FinalImplementation ) );

        IStObjResult? IStObjObjectEngineMap.ToLeaf( Type t ) => _map.GetValueOrDefault( t );

        IReadOnlyList<IStObjResult> IStObjObjectEngineMap.OrderedStObjs => _orderedStObjs ?? Array.Empty<MutableItem>();

        IReadOnlyDictionary<Type, ITypeAttributesCache> IStObjEngineMap.AllTypesAttributesCache => (IReadOnlyDictionary<Type, ITypeAttributesCache>?)_allTypesAttributesCache ?? ImmutableDictionary<Type, ITypeAttributesCache>.Empty;

        internal void SetFinalOrderedResults( IReadOnlyList<MutableItem> ordered, Dictionary<Type, ITypeAttributesCache> allTypesAttributesCache )
        {
            _orderedStObjs = ordered;
            _allTypesAttributesCache = allTypesAttributesCache;
        }

        IStObj? IStObjObjectMap.ToLeaf( Type t ) => _map.GetValueOrDefault( t );

        void IStObjObjectMap.ConfigureServices( in StObjContextRoot.ServiceRegister register )
        {
            throw new NotSupportedException( "ConfigureServices is not supported at build time." );
        }

        /// <summary>
        /// Dynamically projects <see cref="CKTypeCollectorResult.Assemblies"/> to their <see cref="VFeature"/>
        /// (ordered by <see cref="VFeature.Name"/> since by design there can not be multiple versions by feature).
        /// </summary>
        public IReadOnlyCollection<VFeature> Features => _assemblies.Select( ToVFeature ).OrderBy( Util.FuncIdentity ).ToList();

        static VFeature ToVFeature( Assembly a )
        {
            Debug.Assert( a != null );
            Debug.Assert( a.GetName().Name != null );
            var v = InformationalVersion.ReadFromAssembly( a ).Version;
            return new VFeature( a.GetName().Name!, v != null && v.IsValid ? v : SVersion.ZeroVersion );
        }
    }
}
