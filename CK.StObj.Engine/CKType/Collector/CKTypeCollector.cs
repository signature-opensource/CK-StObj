using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Discovers types that support <see cref="IRealObject"/>, <see cref="IAutoService"/>
    /// and <see cref="IPoco"/> marker interfaces.
    /// The <see cref="GetResult"/> method encapsulates the whole work.
    /// </summary>
    public partial class CKTypeCollector
    {
        readonly IActivityMonitor _monitor;
        readonly IDynamicAssembly _tempAssembly;
        readonly IServiceProvider _serviceProvider;
        readonly PocoRegisterer _pocoRegisterer;
        readonly HashSet<Assembly> _assemblies;
        readonly Dictionary<Type, RealObjectClassInfo> _objectCollector;
        readonly List<RealObjectClassInfo> _roots;
        readonly string _mapName;
        readonly Func<IActivityMonitor, Type, bool> _typeFilter;

        /// <summary>
        /// Initializes a new <see cref="CKTypeCollector"/> instance.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="serviceProvider">Service provider used for attribute constructor injection. Must not be null.</param>
        /// <param name="tempAssembly">The temporary <see cref="IDynamicAssembly"/>.</param>
        /// <param name="mapName">Optional map name. Defaults to the empty string.</param>
        /// <param name="typeFilter">Optional type filter.</param>
        public CKTypeCollector(
            IActivityMonitor monitor,
            IServiceProvider serviceProvider,
            IDynamicAssembly tempAssembly,
            Func<IActivityMonitor,Type,bool> typeFilter = null,
            string mapName = null )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( serviceProvider == null ) throw new ArgumentNullException( nameof( serviceProvider ) );
            if( tempAssembly == null ) throw new ArgumentNullException( nameof( tempAssembly ) );
            _monitor = monitor;
            _typeFilter = typeFilter ?? ((m,type) => true);
            _tempAssembly = tempAssembly;
            _serviceProvider = serviceProvider;
            _assemblies = new HashSet<Assembly>();
            _objectCollector = new Dictionary<Type, RealObjectClassInfo>();
            _roots = new List<RealObjectClassInfo>();
            _serviceCollector = new Dictionary<Type, AutoServiceClassInfo>();
            _serviceRoots = new List<AutoServiceClassInfo>();
            _serviceInterfaces = new Dictionary<Type, AutoServiceInterfaceInfo>();
            _kindDetector = new CKTypeKindDetector();
            _pocoRegisterer = new PocoRegisterer( ( m, t ) => (_kindDetector.GetKind( m, t ) & CKTypeKind.IsPoco) != 0, typeFilter: _typeFilter );
            _kindDetector.DefineAsExternalSingleton( monitor, typeof( IPocoFactory<> ) );
            _kindDetector.DefineAsExternalScoped( monitor, typeof( IActivityMonitor ) );
            _mapName = mapName ?? String.Empty;
        }

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
        /// Any type that could be a <see cref="IRealObject"/>, a <see cref="IPoco"/> or a <see cref="IAutoService"/>.
        /// Must not be null.
        /// </param>
        public void RegisterType( Type type )
        {
            if( type == null ) throw new ArgumentNullException( nameof( type ) );
            if( type != typeof( object ) )
            {
                if( type.IsClass )
                {
                    DoRegisterClass( type, out _, out _ );
                }
                else if( _pocoRegisterer.Register( _monitor, type ) )
                {
                    RegisterAssembly( type );
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
            if( c == null ) throw new ArgumentNullException( nameof( c ) );
            if( !c.IsClass ) throw new ArgumentException();
            return c != typeof( object ) ? DoRegisterClass( c, out _, out _ ) : false;
        }

        bool DoRegisterClass( Type t, out RealObjectClassInfo objectInfo, out AutoServiceClassInfo serviceInfo )
        {
            Debug.Assert( t != null && t != typeof( object ) && t.IsClass );

            // Skips already processed types.
            // The object collector contains null RealObjectClassInfo value for already processed types
            // that are skipped or on error.
            serviceInfo = null;
            if( _objectCollector.TryGetValue( t, out objectInfo )
                || _serviceCollector.TryGetValue( t, out serviceInfo ) )
            {
                return false;
            }

            // Registers parent types whatever they are.
            RealObjectClassInfo acParent = null;
            AutoServiceClassInfo sParent = null;
            if( t.BaseType != typeof( object ) ) DoRegisterClass( t.BaseType, out acParent, out sParent );

            CKTypeKind lt = _kindDetector.GetKind( _monitor, t );
            var conflictMsg = lt.GetCKTypeKindCombinationError( true );
            if( conflictMsg != null )
            {
                _monitor.Error( $"Type {t.FullName}: {conflictMsg}." );
            }
            else
            {
                if( acParent != null || (lt & CKTypeKind.RealObject) == CKTypeKind.RealObject )
                {
                    objectInfo = RegisterObjectClassInfo( t, acParent );
                    Debug.Assert( objectInfo != null );
                }
                if( sParent != null || (lt & CKTypeKind.IsAutoService) != 0 )
                {
                    serviceInfo = RegisterServiceClassInfo( t, sParent, lt, objectInfo );
                    Debug.Assert( serviceInfo != null );
                }
            }
            // Marks the type as a registered one.
            if( objectInfo == null && serviceInfo == null )
            {
                _objectCollector.Add( t, null );
            }
            return true;
        }

        RealObjectClassInfo RegisterObjectClassInfo( Type t, RealObjectClassInfo parent )
        {
            RealObjectClassInfo result = new RealObjectClassInfo( _monitor, parent, t, _serviceProvider, _kindDetector, !_typeFilter( _monitor, t ) );
            if( !result.IsExcluded )
            {
                RegisterAssembly( t );
                if( parent == null )
                {
                    Debug.Assert( !_roots.Contains( result ) );
                    _roots.Add( result );
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

        /// <summary>
        /// Obtains the result of the collection.
        /// This is the root of type analysis: the whole system relies on it.
        /// </summary>
        /// <returns>The result object.</returns>
        public CKTypeCollectorResult GetResult()
        {
            using( _monitor.OpenInfo( "Static Type analysis." ) )
            {
                IPocoSupportResult pocoSupport;
                using( _monitor.OpenInfo( "Creating Poco Types and PocoFactory." ) )
                {
                    pocoSupport = _pocoRegisterer.Finalize( _tempAssembly.StubModuleBuilder, _monitor );
                    if( pocoSupport != null )
                    {
                        _tempAssembly.Memory.Add( typeof( IPocoSupportResult ), pocoSupport );
                        _tempAssembly.SourceModules.Add( PocoSourceGenerator.CreateModule( pocoSupport ) );
                        RegisterClass( pocoSupport.FinalFactory );
                    }
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
                return new CKTypeCollectorResult( _assemblies, pocoSupport, contracts, services, _kindDetector );
            }
        }

        RealObjectCollectorResult GetRealObjectResult()
        {
            MutableItem[] allSpecializations = new MutableItem[_roots.Count];
            StObjObjectEngineMap engineMap = new StObjObjectEngineMap( _mapName, allSpecializations, _kindDetector, _assemblies );
            List<List<MutableItem>> concreteClasses = new List<List<MutableItem>>();
            List<IReadOnlyList<Type>> classAmbiguities = null;
            List<Type> abstractTails = new List<Type>();
            var deepestConcretes = new List<(MutableItem, ImplementableTypeInfo)>();
            int idxSpecialization = 0;

            Debug.Assert( _roots.All( info => info != null && !info.IsExcluded && info.Generalization == null),
                "_roots contains only not Excluded types." );
            foreach( RealObjectClassInfo newOne in _roots )
            {
                deepestConcretes.Clear();
                newOne.CreateMutableItemsPath( _monitor, _serviceProvider, engineMap, null, _tempAssembly, deepestConcretes, abstractTails );
                if( deepestConcretes.Count == 1 )
                {
                    MutableItem last = deepestConcretes[0].Item1;
                    allSpecializations[idxSpecialization++] = last;
                    var path = new List<MutableItem>();
                    last.InitializeBottomUp( null, deepestConcretes[0].Item2 );
                    path.Add( last );
                    MutableItem spec = last, toInit = last;
                    while( (toInit = toInit.Generalization) != null )
                    {
                        toInit.InitializeBottomUp( spec, null );
                        path.Add( toInit );
                        spec = toInit;
                    }
                    path.Reverse();
                    concreteClasses.Add( path );
                    foreach( var m in path ) engineMap.AddClassMapping( m.Type.Type, last );
                }
                else if( deepestConcretes.Count > 1 )
                {
                    List<Type> ambiguousPath = new List<Type>() { newOne.Type };
                    ambiguousPath.AddRange( deepestConcretes.Select( m => m.Item1.Type.Type ) );

                    if( classAmbiguities == null ) classAmbiguities = new List<IReadOnlyList<Type>>();
                    classAmbiguities.Add( ambiguousPath.ToArray() );
                }
            }
            Debug.Assert( classAmbiguities != null || idxSpecialization == allSpecializations.Length );
            Dictionary<Type, List<Type>> interfaceAmbiguities = null;
            foreach( var path in concreteClasses )
            {
                MutableItem finalType = path[path.Count - 1];
                foreach( var item in path )
                {
                    foreach( Type itf in item.Type.EnsureThisAmbientInterfaces( _monitor, _kindDetector ) )
                    {
                        MutableItem alreadyMapped;
                        if( (alreadyMapped = engineMap.ToLeaf( itf )) != null )
                        {
                            if( interfaceAmbiguities == null )
                            {
                                interfaceAmbiguities = new Dictionary<Type, List<Type>>();
                                interfaceAmbiguities.Add( itf, new List<Type>() { itf, alreadyMapped.Type.Type, item.Type.Type } );
                            }
                            else
                            {
                                var list = interfaceAmbiguities.GetOrSet( itf, t => new List<Type>() { itf, alreadyMapped.Type.Type } );
                                list.Add( item.Type.Type );
                            }
                        }
                        else
                        {
                            engineMap.AddInterfaceMapping( itf, item, finalType );
                        }
                    }
                }
            }
            return new RealObjectCollectorResult(
                engineMap,
                concreteClasses,
                classAmbiguities != null
                    ? classAmbiguities
                    : (IReadOnlyList<IReadOnlyList<Type>>)Array.Empty<IReadOnlyList<Type>>(),
                interfaceAmbiguities != null
                    ? interfaceAmbiguities.Values.Select( list => list.ToArray() ).ToArray()
                    : Array.Empty<IReadOnlyList<Type>>(),
                abstractTails );
        }

    }

}
