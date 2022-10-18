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
    /// Discovers types that support <see cref="IRealObject"/>, <see cref="IAutoService"/>
    /// and <see cref="IPoco"/> marker interfaces.
    /// The <see cref="GetResult"/> method encapsulates the whole work.
    /// </summary>
    public partial class CKTypeCollector : IAutoServiceKindComputeFacade
    {
        readonly IActivityMonitor _monitor;
        readonly IDynamicAssembly _tempAssembly;
        readonly IServiceProvider _serviceProvider;
        readonly PocoDirectoryBuilder _pocoBuilder;
        readonly HashSet<Assembly> _assemblies;
        readonly Dictionary<Type, RealObjectClassInfo?> _objectCollector;
        readonly Dictionary<Type, TypeAttributesCache?> _regularTypeCollector;
        readonly List<RealObjectClassInfo> _roots;
        readonly Func<IActivityMonitor, Type, bool> _typeFilter;
        readonly IReadOnlyList<string> _names;

        /// <summary>
        /// Initializes a new <see cref="CKTypeCollector"/> instance.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="serviceProvider">Service provider used for attribute constructor injection. Must not be null.</param>
        /// <param name="tempAssembly">The temporary <see cref="IDynamicAssembly"/>.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        /// <param name="names">Optional list of names for the final StObjMap. When null or empty, a single empty string is the default name.</param>
        public CKTypeCollector( IActivityMonitor monitor,
                                IServiceProvider serviceProvider,
                                IDynamicAssembly tempAssembly,
                                Func<IActivityMonitor, Type, bool>? typeFilter = null,
                                IEnumerable<string>? names = null )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( serviceProvider );
            Throw.CheckNotNullArgument( tempAssembly );
            _monitor = monitor;
            _typeFilter = typeFilter ?? ((m,type) => type.FullName != null);
            _tempAssembly = tempAssembly;
            _serviceProvider = serviceProvider;
            _assemblies = new HashSet<Assembly>();
            _objectCollector = new Dictionary<Type, RealObjectClassInfo?>();
            _regularTypeCollector = new Dictionary<Type, TypeAttributesCache?>();
            _roots = new List<RealObjectClassInfo>();
            _serviceCollector = new Dictionary<Type, AutoServiceClassInfo>();
            _serviceRoots = new List<AutoServiceClassInfo>();
            _serviceInterfaces = new Dictionary<Type, AutoServiceInterfaceInfo?>();
            _multipleMappings = new Dictionary<Type, MultipleImpl>();
            KindDetector = new CKTypeKindDetector( typeFilter );
            _pocoBuilder = new PocoDirectoryBuilder( ( m, t ) => (KindDetector.GetValidKind( m, t ) & CKTypeKind.IsPoco) != 0, typeFilter: _typeFilter );
            _names = names == null || !names.Any() ? new[] { String.Empty } : names.ToArray();
        }

        /// <summary>
        /// Exposes the <see cref="Setup.CKTypeKindDetector"/>.
        /// </summary>
        public CKTypeKindDetector KindDetector { get; }

        /// <summary>
        /// Gets the number of registered types.
        /// </summary>
        public int RegisteredTypeCount => _objectCollector.Count;

        /// <summary>
        /// Registers multiple types.
        /// </summary>
        /// <param name="types">Set of types to register.</param>
        public void RegisterTypes( IEnumerable<Type> types )
        {
            if( types == null ) throw new ArgumentNullException( "types" );
            foreach( var t in types )
            {
                if( t != null && t != typeof( object ) ) RegisterType( t );
            }
        }

        /// <summary>
        /// Registers a type.
        /// </summary>
        /// <param name="type">
        /// Any type that could be a <see cref="IRealObject"/>, a <see cref="IPoco"/> or a <see cref="IAutoService"/>
        /// or a type decorated by some attributes.
        /// </param>
        public void RegisterType( Type type )
        {
            Throw.CheckNotNullArgument( type );
            if( type != typeof( object ) )
            {
                if( type.IsClass )
                {
                    DoRegisterClass( type, out _, out _ );
                }
                else if( type.IsInterface )
                {
                    if( _pocoBuilder.RegisterInterface( _monitor, type ) )
                    {
                        RegisterAssembly( type );
                    }
                    RegisterRegularType( type );
                }
                else
                {
                    RegisterRegularType( type );
                }
            }
        }

        /// <summary>
        /// Registers a class.
        /// It must be a class otherwise an argument exception is thrown.
        /// </summary>
        /// <param name="c">Class to register.</param>
        /// <returns>True if it is a new class for this collector, false if it has already been registered.</returns>
        public bool RegisterClass( Type c )
        {
            Throw.CheckArgument( c?.IsClass is true );
            return c != typeof( object ) ? DoRegisterClass( c, out _, out _ ) : false;
        }

        bool DoRegisterClass( Type t, out RealObjectClassInfo? objectInfo, out AutoServiceClassInfo? serviceInfo )
        {
            Debug.Assert( t != null && t != typeof( object ) && t.IsClass );

            // Skips already processed types.
            // The object collectors contains null RealObjectClassInfo and AutoServiceClassInfo value for
            // already processed types that are skipped or on error.
            serviceInfo = null;
            if( _objectCollector.TryGetValue( t, out objectInfo )
                || _serviceCollector.TryGetValue( t, out serviceInfo ) )
            {
                return false;
            }

            // Registers parent types whatever they are.
            RealObjectClassInfo? acParent = null;
            AutoServiceClassInfo? sParent = null;
            if( t.BaseType != typeof( object ) )
            {
                Debug.Assert( t.BaseType != null, "Since t is not 'object'." );
                DoRegisterClass( t.BaseType, out acParent, out sParent );
            }
            CKTypeKind lt = KindDetector.GetRawKind( _monitor, t );
            if( (lt & CKTypeKind.HasCombinationError) == 0 )
            {
                bool isExcluded = (lt & CKTypeKind.IsExcludedType) != 0;
                if( acParent != null || (lt & CKTypeKind.RealObject) == CKTypeKind.RealObject )
                {
                    objectInfo = RegisterObjectClassInfo( t, isExcluded, acParent );
                    Debug.Assert( objectInfo != null );
                }
                if( sParent != null || (lt & CKTypeKind.IsAutoService) != 0 )
                {
                    serviceInfo = RegisterServiceClassInfo( t, isExcluded, sParent, objectInfo );
                    Debug.Assert( serviceInfo != null );
                }
            }
            // Marks the type as a registered one and gives it a chance to carry
            // Attributes...
            if( objectInfo == null && serviceInfo == null )
            {
                _objectCollector.Add( t, null );
                if( (lt & CKTypeKind.IsExcludedType) == 0 ) RegisterRegularType( t );
            }
            return true;
        }

        RealObjectClassInfo RegisterObjectClassInfo( Type t, bool isExcluded, RealObjectClassInfo? parent )
        {
            RealObjectClassInfo result = new RealObjectClassInfo( _monitor, parent, t, _serviceProvider, isExcluded );
            if( !result.IsExcluded )
            {
                RegisterAssembly( t );
                if( parent == null )
                {
                    Debug.Assert( !_roots.Contains( result ) );
                    _roots.Add( result );
                    // This is were the IRealObject interfaces could be enlisted.
                }
            }
            _objectCollector.Add( t, result );
            return result;
        }

        /// <summary>
        /// Registers an assembly for which at least one type has been handled.
        /// This is required for code generation: such assemblies are dependencies.
        /// </summary>
        /// <param name="t">The registered type.</param>
        protected void RegisterAssembly( Type t )
        {
            var a = t.Assembly;
            if( !a.IsDynamic ) _assemblies.Add( a );
        }

        void RegisterRegularType( Type t )
        {
            if( !_regularTypeCollector.ContainsKey( t ) )
            {
                var c = TypeAttributesCache.CreateOnRegularType( _monitor, _serviceProvider, t );
                _regularTypeCollector.Add( t, c );
                if( c != null )
                {
                    _monitor.Trace( $"At least one bound attribute on '{t}' has been registered." );
                    RegisterAssembly( t );
                }
            }
        }

        /// <summary>
        /// Obtains the result of the collection.
        /// This is the root of type analysis: the whole system relies on it.
        /// </summary>
        /// <returns>The result object.</returns>
        public CKTypeCollectorResult GetResult()
        {
            using( _monitor.OpenInfo( "Static Type analysis." ) )
            {
                IPocoDirectory? pocoDirectory;
                PocoTypeSystem? pocoTypeSystem;
                using( _monitor.OpenInfo( "Creating Poco Types and PocoFactory." ) )
                {
                    pocoDirectory = _pocoBuilder.Build( _tempAssembly, _monitor );
                    if( pocoDirectory != null )
                    {
                        _tempAssembly.Memory.Add( typeof( IPocoDirectory ), pocoDirectory );
                        RegisterClass( typeof( PocoDirectory ) );
                        foreach( var c in pocoDirectory.Families ) RegisterClass( c.PocoFactoryClass );
                    }
                    else
                    {
                        // On error, we register the Empty result.
                        _tempAssembly.Memory.Add( typeof( IPocoDirectory ), pocoDirectory = EmptyPocoDirectory.Default );
                    }
                    Debug.Assert( _tempAssembly.GetPocoDirectory() == pocoDirectory, "The extension method GetPocoDirectory() provides it." );
                }
                using( _monitor.OpenInfo( "Initializing Poco Type System." ) )
                {
                    pocoTypeSystem = new PocoTypeSystem();
                    if( !pocoTypeSystem.Initialize( pocoDirectory, _monitor ) )
                    {
                        _monitor.CloseGroup( "Failed" );
                    }
                    _tempAssembly.Memory.Add( typeof( IPocoTypeSystem ), pocoTypeSystem );
                    Debug.Assert( _tempAssembly.GetPocoTypeSystem() == pocoTypeSystem, "The extension method GetPocoTypeSystem() provides it." );
                }
                RealObjectCollectorResult contracts;
                using( _monitor.OpenInfo( "Real objects handling." ) )
                {
                    contracts = GetRealObjectResult();
                    Debug.Assert( contracts != null );
                }
                AutoServiceCollectorResult services;
                using( _monitor.OpenInfo( "Auto services handling." ) )
                {
                    services = GetAutoServiceResult( contracts );
                }
                return new CKTypeCollectorResult( _assemblies, pocoDirectory, contracts, services, _regularTypeCollector, this );
            }
        }

        RealObjectCollectorResult GetRealObjectResult()
        {
            List<MutableItem> allSpecializations = new List<MutableItem>( _roots.Count );
            StObjObjectEngineMap engineMap = new StObjObjectEngineMap( _names, allSpecializations, KindDetector, _assemblies );
            List<List<MutableItem>> concreteClasses = new List<List<MutableItem>>();
            List<IReadOnlyList<Type>>? classAmbiguities = null;
            List<Type> abstractTails = new List<Type>();
            var deepestConcretes = new List<(MutableItem, ImplementableTypeInfo)>();

            Debug.Assert( _roots.All( info => info != null && !info.IsExcluded && info.Generalization == null),
                "_roots contains only not Excluded types." );
            foreach( RealObjectClassInfo newOne in _roots )
            {
                deepestConcretes.Clear();
                newOne.CreateMutableItemsPath( _monitor, _serviceProvider, engineMap, null, _tempAssembly, deepestConcretes, abstractTails );
                if( deepestConcretes.Count == 1 )
                {
                    MutableItem last = deepestConcretes[0].Item1;
                    allSpecializations.Add( last );
                    var path = new List<MutableItem>();
                    last.InitializeBottomUp( null );
                    path.Add( last );
                    MutableItem? spec = last, toInit = last;
                    while( (toInit = toInit.Generalization) != null )
                    {
                        toInit.InitializeBottomUp( spec );
                        path.Add( toInit );
                        spec = toInit;
                    }
                    path.Reverse();
                    concreteClasses.Add( path );
                    foreach( var m in path ) engineMap.AddClassMapping( m.RealObjectType.Type, last );
                }
                else if( deepestConcretes.Count > 1 )
                {
                    List<Type> ambiguousPath = new List<Type>() { newOne.Type };
                    ambiguousPath.AddRange( deepestConcretes.Select( m => m.Item1.RealObjectType.Type ) );

                    if( classAmbiguities == null ) classAmbiguities = new List<IReadOnlyList<Type>>();
                    classAmbiguities.Add( ambiguousPath.ToArray() );
                }
            }
            Dictionary<Type, List<Type>>? interfaceAmbiguities = null;
            foreach( var path in concreteClasses )
            {
                MutableItem finalType = path[path.Count - 1];
                finalType.RealObjectType.InitializeInterfaces( _monitor, this );
                foreach( var item in path )
                {
                    foreach( Type itf in item.RealObjectType.ThisRealObjectInterfaces )
                    {
                        MutableItem? alreadyMapped;
                        if( (alreadyMapped = engineMap.RawMappings.GetValueOrDefault( itf )) != null )
                        {
                            if( interfaceAmbiguities == null )
                            {
                                interfaceAmbiguities = new Dictionary<Type, List<Type>>
                                {
                                    { itf, new List<Type>() { itf, alreadyMapped.RealObjectType.Type, item.RealObjectType.Type } }
                                };
                            }
                            else
                            {
                                var list = interfaceAmbiguities.GetOrSet( itf, t => new List<Type>() { itf, alreadyMapped.RealObjectType.Type } );
                                list.Add( item.RealObjectType.Type );
                            }
                        }
                        else
                        {
                            engineMap.AddInterfaceMapping( itf, item, finalType );
                        }
                    }
                }
            }
            return new RealObjectCollectorResult( engineMap,
                                                  concreteClasses,
                                                  classAmbiguities ?? (IReadOnlyList<IReadOnlyList<Type>>)Array.Empty<IReadOnlyList<Type>>(),
                                                  interfaceAmbiguities != null
                                                    ? interfaceAmbiguities.Values.Select( list => list.ToArray() ).ToArray()
                                                    : Array.Empty<IReadOnlyList<Type>>(),
                                                  abstractTails );
        }

    }

}
