using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Setup;
using CK.Core;
using System.Data;

namespace CK.Setup;

public partial class CKTypeCollector
{
    readonly Dictionary<Type, AutoServiceClassInfo> _serviceCollector;
    readonly List<AutoServiceClassInfo> _serviceRoots;
    readonly Dictionary<Type, AutoServiceInterfaceInfo?> _serviceInterfaces;
    int _serviceInterfaceCount;
    int _serviceRootInterfaceCount;

    AutoServiceClassInfo RegisterServiceClassInfo( IActivityMonitor monitor, Type t, bool isExcluded, AutoServiceClassInfo? parent, RealObjectClassInfo? objectInfo )
    {
        var serviceInfo = new AutoServiceClassInfo( monitor, _serviceProvider, parent, t, isExcluded, objectInfo, _alsoRegister );
        if( !serviceInfo.TypeInfo.IsExcluded )
        {
            RegisterAssembly( monitor, t, isVFeature: true );
            if( serviceInfo.Generalization == null ) _serviceRoots.Add( serviceInfo );
        }
        _serviceCollector.Add( t, serviceInfo );
        return serviceInfo;
    }

    bool IsAutoService( IActivityMonitor monitor, Type t ) => (KindDetector.GetNonDefinerKind( monitor, t ) & CKTypeKind.IsAutoService) != 0;

    internal AutoServiceClassInfo? FindServiceClassInfo( IActivityMonitor monitor, Type t )
    {
        Throw.DebugAssert( IsAutoService( monitor, t ) && t.IsClass );
        _serviceCollector.TryGetValue( t, out var info );
        return info;
    }

    internal AutoServiceInterfaceInfo? FindServiceInterfaceInfo( IActivityMonitor monitor, Type t )
    {
        Throw.DebugAssert( IsAutoService( monitor, t ) && t.IsInterface );
        _serviceInterfaces.TryGetValue( t, out var info );
        return info;
    }

    /// <summary>
    /// Returns null if and only if the interface type is excluded.
    /// This is only called by RegisterServiceInterfaces below (and recursively calls it).
    /// </summary>
    AutoServiceInterfaceInfo? RegisterServiceInterface( IActivityMonitor monitor, Type t, CKTypeKind lt )
    {
        Throw.DebugAssert( t.IsInterface && lt == KindDetector.GetNonDefinerKind( monitor, t ) );
        if( !_serviceInterfaces.TryGetValue( t, out var info ) )
        {
            if( (lt & CKTypeKind.IsExcludedType) == 0 )
            {
                var attr = new TypeAttributesCache( monitor, t, _serviceProvider, _alsoRegister );
                info = new AutoServiceInterfaceInfo( attr, lt, RegisterServiceInterfaces( monitor, t.GetInterfaces() ) );
                ++_serviceInterfaceCount;
                if( info.Interfaces.Count == 0 ) ++_serviceRootInterfaceCount;
            }
            // Adds a null value when filtered out.
            _serviceInterfaces.Add( t, info );
        }
        return info;
    }

    internal IEnumerable<AutoServiceInterfaceInfo> RegisterServiceInterfaces( IActivityMonitor monitor,
                                                                              IEnumerable<Type> interfaces,
                                                                              Action<IActivityMonitor, Type, CKTypeKind, CKTypeCollector>? multipleImplementation = null )
    {
        foreach( var iT in interfaces )
        {
            CKTypeKind k = KindDetector.GetNonDefinerKind( monitor, iT );
            if( (k & CKTypeKind.HasError) == 0 )
            {
                if( (k & CKTypeKind.IsMultipleService) != 0 )
                {
                    multipleImplementation?.Invoke( monitor, iT, k, this );
                }
                else if( (k & CKTypeKind.IsAutoService) != 0 )
                {
                    var r = RegisterServiceInterface( monitor, iT, k );
                    if( r != null ) yield return r;
                }
            }
        }
    }

    AutoServiceCollectorResult GetAutoServiceResult( IActivityMonitor monitor, RealObjectCollectorResult contracts )
    {
        bool success = true;
        List<Type>? abstractTails = null;
        success &= InitializeRootServices( monitor, contracts.EngineMap, out var classAmbiguities, ref abstractTails );
        List<AutoServiceClassInfo> subGraphs = new List<AutoServiceClassInfo>();
        if( success && classAmbiguities == null )
        {
            foreach( var s in _serviceRoots )
            {
                s.FinalizeMostSpecializedAndCollectSubGraphs( subGraphs );
            }
            Throw.DebugAssert( _serviceRoots.All( c => c.MostSpecialized != null ) );
            Throw.DebugAssert( subGraphs.All( c => c.MostSpecialized != null ) );
        }
        // Collecting all, roots and leaves interfaces.
        var leafInterfaces = new List<AutoServiceInterfaceInfo>();
        var allInterfaces = new AutoServiceInterfaceInfo[_serviceInterfaceCount];
        var rootInterfaces = new AutoServiceInterfaceInfo[_serviceRootInterfaceCount];
        int idxAll = 0;
        int idxRoot = 0;
        foreach( var it in _serviceInterfaces.Values )
        {
            if( it != null )
            {
                allInterfaces[idxAll++] = it;
                if( !it.IsSpecialized ) leafInterfaces.Add( it );
                if( it.Interfaces.Count == 0 ) rootInterfaces[idxRoot++] = it;
            }
        }
        Throw.DebugAssert( idxAll == allInterfaces.Length );
        Throw.DebugAssert( idxRoot == rootInterfaces.Length );
        monitor.Info( $"{allInterfaces.Length} Service interfaces with {rootInterfaces.Length} roots and {leafInterfaces.Count} interface leaves." );
        return new AutoServiceCollectorResult( success,
                                               allInterfaces,
                                               leafInterfaces,
                                               rootInterfaces,
                                               _serviceRoots,
                                               classAmbiguities,
                                               abstractTails,
                                               subGraphs );
    }

    bool InitializeRootServices( IActivityMonitor monitor,
                                 StObjObjectEngineMap engineMap,
                                 out IReadOnlyList<IReadOnlyList<AutoServiceClassInfo>>? classAmbiguities,
                                 ref List<Type>? abstractTails )
    {
        using( monitor.OpenInfo( $"Analyzing {_serviceRoots.Count} Service class hierarchies." ) )
        {
            bool error = false;
            var deepestConcretes = new List<AutoServiceClassInfo>();
            Throw.DebugAssert( "_serviceRoots contains only not Excluded types.",
                                _serviceRoots.All( info => info != null && !info.TypeInfo.IsExcluded && info.Generalization == null ) );
            List<(AutoServiceClassInfo Root, AutoServiceClassInfo[] Leaves)>? ambiguities = null;
            // We must wait until all paths have been initialized before ensuring constructor parameters
            AutoServiceClassInfo[] resolvedLeaves = new AutoServiceClassInfo[_serviceRoots.Count];
            for( int i = 0; i < _serviceRoots.Count; ++i )
            {
                var c = _serviceRoots[i];
                deepestConcretes.Clear();
                if( !c.InitializePath( monitor, this, _tempAssembly, deepestConcretes, ref abstractTails ) )
                {
                    monitor.Warn( $"Service '{c.ClassType}' is abstract. It is ignored." );
                    _serviceRoots.RemoveAt( i-- );
                    continue;
                }
                // If deepestConcretes is empty it means that the whole chain is purely abstract.
                // We ignore it.
                if( deepestConcretes.Count == 1 )
                {
                    // No specialization ambiguities: no class unification required.
                    resolvedLeaves[i] = deepestConcretes[0];
                }
                else if( deepestConcretes.Count > 1 )
                {
                    if( ambiguities == null ) ambiguities = new List<(AutoServiceClassInfo, AutoServiceClassInfo[])>();
                    ambiguities.Add( (c, deepestConcretes.ToArray()) );
                }
            }
            monitor.Trace( $"Found {_serviceRoots.Count} unambiguous paths." );
            // Initializes all non ambiguous paths.
            for( int i = 0; i < _serviceRoots.Count; ++i )
            {
                var leaf = resolvedLeaves[i];
                if( leaf != null )
                {
                    // Here, calling leaf.EnsureCtorBinding() would be enough but Service Resolution requires
                    // the closure on all leaves. GetCtorParametersClassClosure calls EnsureCtorBinding.
                    leaf.GetCtorParametersClassClosure( monitor, this, ref error );
                    error |= !_serviceRoots[i].SetMostSpecialized( monitor, engineMap, leaf );
                }
            }
            // Every non ambiguous paths have been initialized.
            // Now, if there is no initialization error, tries to resolve class ambiguities.
            List<IReadOnlyList<AutoServiceClassInfo>>? remainingAmbiguities = null;
            if( !error && ambiguities != null )
            {
                using( monitor.OpenInfo( $"Trying to resolve {ambiguities.Count} ambiguities." ) )
                {
                    var resolver = new ClassAmbiguityResolver( monitor, this, engineMap );
                    foreach( var a in ambiguities )
                    {
                        using( monitor.OpenTrace( $"Trying to resolve class ambiguities for {a.Root.ClassType}." ) )
                        {
                            var (success, initError) = resolver.TryClassUnification( a.Root, a.Leaves );
                            error |= initError;
                            if( success )
                            {
                                Throw.DebugAssert( "This is null only on error.", a.Root.MostSpecialized != null );
                                monitor.CloseGroup( "Succeeds, resolved to: " + a.Root.MostSpecialized.ClassType );
                            }
                            else
                            {
                                monitor.CloseGroup( "Failed." );
                                if( remainingAmbiguities == null ) remainingAmbiguities = new List<IReadOnlyList<AutoServiceClassInfo>>();
                                resolver.CollectRemainingAmbiguities( remainingAmbiguities );
                            }
                        }
                    }
                }
            }
            classAmbiguities = remainingAmbiguities;
            return !error;
        }
    }

    class ClassAmbiguityResolver
    {
        readonly Dictionary<AutoServiceClassInfo, ClassAmbiguity> _ambiguities;
        readonly IActivityMonitor _monitor;
        readonly CKTypeCollector _collector;
        readonly StObjObjectEngineMap _engineMap;

        AutoServiceClassInfo? _root;
        AutoServiceClassInfo? _rootAmbiguity;
        AutoServiceClassInfo[]? _allLeaves;

        readonly struct ClassAmbiguity
        {
            public readonly AutoServiceClassInfo Class;
            public readonly List<AutoServiceClassInfo> Leaves;

            public ClassAmbiguity( AutoServiceClassInfo c )
            {
                Throw.DebugAssert( c.TypeInfo.SpecializationsCount > 0 && c.MostSpecialized == null );
                Class = c;
                Leaves = new List<AutoServiceClassInfo>();
            }
        }

        public ClassAmbiguityResolver( IActivityMonitor monitor, CKTypeCollector collector, StObjObjectEngineMap engineMap )
        {
            _ambiguities = new Dictionary<AutoServiceClassInfo, ClassAmbiguity>();
            _monitor = monitor;
            _collector = collector;
            _engineMap = engineMap;
        }

        public (bool Success, bool InitializationError) TryClassUnification( AutoServiceClassInfo root, AutoServiceClassInfo[] allLeaves )
        {
            Throw.DebugAssert( allLeaves.Length > 1
                              && NextUpperAmbiguity( allLeaves[0] ) != null
                              && NextUpperAmbiguity( allLeaves[1] ) != null );
            _root = root;
            _allLeaves = allLeaves;
            while( root.TypeInfo.SpecializationsCount == 1 )
            {
                root = root.Specializations.Single();
            }
            _rootAmbiguity = root;
            bool allSuccess = true;
            bool initializationError = !Initialize();
            foreach( var ca in _ambiguities.Values.OrderByDescending( a => a.Class.SpecializationDepth ) )
            {
                var (success, initError) = Resolve( ca );
                initializationError |= initError;
                allSuccess &= success;
            }
            if( allSuccess
                && !initializationError
                && _root != _rootAmbiguity )
            {
                Throw.DebugAssert( _root.MostSpecialized == null );
                Throw.DebugAssert( _rootAmbiguity.MostSpecialized != null );
                initializationError |= !_root.SetMostSpecialized( _monitor, _engineMap, _rootAmbiguity.MostSpecialized );
            }
            return (allSuccess, initializationError);
        }

        public void CollectRemainingAmbiguities( List<IReadOnlyList<AutoServiceClassInfo>> ambiguities )
        {
            Throw.DebugAssert( _ambiguities.Count > 0 );
            foreach( var ca in _ambiguities.Values )
            {
                if( ca.Class.MostSpecialized == null )
                {
                    ca.Leaves.Insert( 0, ca.Class );
                    ambiguities.Add( ca.Leaves );
                }
            }
            _ambiguities.Clear();
        }

        bool Initialize()
        {
            Throw.DebugAssert( "TryClassUnification initialized it.", _allLeaves != null );
            _ambiguities.Clear();
            bool initializationError = false;
            foreach( var leaf in _allLeaves )
            {
                leaf.GetCtorParametersClassClosure( _monitor, _collector, ref initializationError );
                var a = NextUpperAmbiguity( leaf );
                while( a != null )
                {
                    if( !_ambiguities.TryGetValue( a, out ClassAmbiguity ca ) )
                    {
                        ca = new ClassAmbiguity( a );
                        _ambiguities.Add( a, ca );
                    }
                    ca.Leaves.Add( leaf );
                    a = NextUpperAmbiguity( a );
                }
            }
            return !initializationError;
        }

        (bool Success, bool InitializationError) Resolve( ClassAmbiguity ca )
        {
            bool success = false;
            bool initializationError = false;
#if DEBUG
            // Count is used to assert the fact that not 2 leaves should match.
            int resolvedPathCount = 0;
#endif
            var a = ca.Class;
            foreach( var leaf in ca.Leaves )
            {
                bool thisPathIsResolved = true;
                var closure = leaf.ComputedCtorParametersClassClosure;
                bool isLeafUnifier = a.Specializations
                                        .Where( s => !s.TypeInfo.IsAssignableFrom( leaf.TypeInfo ) )
                                        .All( s => closure.Contains( s ) );
                if( isLeafUnifier )
                {
                    if( a.MostSpecialized != null )
                    {
                        _monitor.Error( $"Class Unification ambiguity: '{a.ClassType}' is already resolved by '{a.MostSpecialized.ClassType}'. It can not be resolved also by '{leaf.ClassType}'." );
                        thisPathIsResolved = false;
                    }
                    else
                    {
                        _monitor.Trace( $"Class Unification: '{a.ClassType}' resolved to '{leaf.ClassType}'." );
                        initializationError |= !a.SetMostSpecialized( _monitor, _engineMap, leaf );
                    }
                }
                else
                {
                    thisPathIsResolved = false;
                }
#if DEBUG
                // If this leaf worked, it must be the very first one: subsequent ones must fail.
                Throw.DebugAssert( !thisPathIsResolved || ++resolvedPathCount == 1 );
#endif
                success |= thisPathIsResolved;
            }
            if( !success )
            {
                _monitor.Error( $"Service Class Unification: unable to resolve '{a.ClassType}' to a unique specialization." );
            }
            return (success, initializationError);
        }

        static AutoServiceClassInfo? NextUpperAmbiguity( AutoServiceClassInfo start )
        {
            var g = start.Generalization;
            while( g != null )
            {
                if( g.TypeInfo.SpecializationsCount > 1 ) break;
                g = g.Generalization;
            }
            return g;
        }
    }
}
