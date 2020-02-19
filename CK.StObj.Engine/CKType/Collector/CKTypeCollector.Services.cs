using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using CK.Setup;
using CK.Core;

namespace CK.Setup
{
    public partial class CKTypeCollector
    {
        readonly Dictionary<Type, AutoServiceClassInfo> _serviceCollector;
        readonly List<AutoServiceClassInfo> _serviceRoots;
        readonly Dictionary<Type, AutoServiceInterfaceInfo> _serviceInterfaces;
        readonly CKTypeKindDetector _kindDetector;
        int _serviceInterfaceCount;
        int _serviceRootInterfaceCount;

        AutoServiceClassInfo RegisterServiceClassInfo( Type t, AutoServiceClassInfo parent, CKTypeKind lt, RealObjectClassInfo objectInfo )
        {
            var serviceInfo = new AutoServiceClassInfo( _monitor, _serviceProvider, parent, t, this, !_typeFilter( _monitor, t ), lt, objectInfo );
            if( !serviceInfo.TypeInfo.IsExcluded )
            {
                RegisterAssembly( t );
                if( serviceInfo.Generalization == null ) _serviceRoots.Add( serviceInfo );
            }
            _serviceCollector.Add( t, serviceInfo );
            return serviceInfo;
        }

        /// <summary>
        /// Exposes the <see cref="CKTypeKindDetector"/>.
        /// </summary>
        public CKTypeKindDetector AmbientKindDetector => _kindDetector;

        bool IsAutoService( Type t ) => (_kindDetector.GetKind( _monitor, t ) & CKTypeKind.IsAutoService) != 0;

        internal AutoServiceClassInfo FindServiceClassInfo( Type t )
        {
            Debug.Assert( IsAutoService( t ) && t.IsClass );
            _serviceCollector.TryGetValue( t, out var info );
            return info;
        }

        internal AutoServiceInterfaceInfo FindServiceInterfaceInfo( Type t )
        {
            Debug.Assert( IsAutoService( t ) && t.IsInterface );
            _serviceInterfaces.TryGetValue( t, out var info );
            return info;
        }

        /// <summary>
        /// Returns null if and only if the interface type is excluded.
        /// </summary>
        AutoServiceInterfaceInfo RegisterServiceInterface( Type t, CKTypeKind lt )
        {
            Debug.Assert( t.IsInterface
                            && lt == _kindDetector.GetKind( _monitor, t )
                            && (lt == CKTypeKind.IsAutoService
                                || lt == CKTypeKind.AutoSingleton
                                || lt == CKTypeKind.AutoScoped) );
            if( !_serviceInterfaces.TryGetValue( t, out var info ) )
            {
                if( _typeFilter( _monitor, t ) )
                {
                    info = new AutoServiceInterfaceInfo( t, lt, RegisterServiceInterfaces( t.GetInterfaces() ) );
                    ++_serviceInterfaceCount;
                    if( info.Interfaces.Count == 0 ) ++_serviceRootInterfaceCount;
                }
                // Adds a null value when filtered out.
                _serviceInterfaces.Add( t, info );
            }
            return info;
        }

        internal IEnumerable<AutoServiceInterfaceInfo> RegisterServiceInterfaces( IEnumerable<Type> interfaces )
        {
            foreach( var iT in interfaces )
            {
                CKTypeKind lt = _kindDetector.GetKind( _monitor, iT );
                var conflictMsg = lt.GetCKTypeKindCombinationError();
                if( conflictMsg != null )
                {
                    _monitor.Error( $"Interface '{iT.FullName}': {conflictMsg}" );
                }
                else if( (lt&CKTypeKind.IsAutoService) != 0 )
                {
                    var r = RegisterServiceInterface( iT, lt );
                    if( r != null ) yield return r;
                }
            }
        }

        AutoServiceCollectorResult GetAutoServiceResult( RealObjectCollectorResult contracts )
        {
            bool success = true;
            List<Type> abstractTails = null;
            success &= InitializeRootServices( contracts.EngineMap, out var classAmbiguities, ref abstractTails );
            List<AutoServiceClassInfo> subGraphs = new List<AutoServiceClassInfo>();
            if( success && classAmbiguities == null )
            {
                foreach( var s in _serviceRoots )
                {
                    s.FinalizeMostSpecializedAndCollectSubGraphs( subGraphs );
                }
                Debug.Assert( _serviceRoots.All( c => c.MostSpecialized != null ) );
                Debug.Assert( subGraphs.All( c => c.MostSpecialized != null ) );
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
            Debug.Assert( idxAll == allInterfaces.Length );
            Debug.Assert( idxRoot == rootInterfaces.Length );
            _monitor.Info( $"{allInterfaces.Length} Service interfaces with {rootInterfaces.Length} roots and {leafInterfaces.Count} interface leaves." );
            return new AutoServiceCollectorResult(
                success,
                allInterfaces,
                leafInterfaces,
                rootInterfaces,
                _serviceRoots,
                classAmbiguities,
                abstractTails,
                subGraphs );
        }

        bool InitializeRootServices(
            StObjObjectEngineMap engineMap,
            out IReadOnlyList<IReadOnlyList<AutoServiceClassInfo>> classAmbiguities,
            ref List<Type> abstractTails )
        {
            using( _monitor.OpenInfo( $"Analysing {_serviceRoots.Count} Service class hierarchies." ) )
            {
                bool error = false;
                var deepestConcretes = new List<AutoServiceClassInfo>();
                Debug.Assert( _serviceRoots.All( info => info != null && !info.TypeInfo.IsExcluded && info.Generalization == null ),
                    "_serviceRoots contains only not Excluded types." );
                List<(AutoServiceClassInfo Root, AutoServiceClassInfo[] Leaves)> ambiguities = null;
                // We must wait until all paths have been initialized before ensuring constructor parameters
                AutoServiceClassInfo[] resolvedLeaves = new AutoServiceClassInfo[_serviceRoots.Count];
                for( int i = 0; i < _serviceRoots.Count; ++i )
                {
                    var c = _serviceRoots[i];
                    deepestConcretes.Clear();
                    if( !c.InitializePath( _monitor, this, null, _tempAssembly, deepestConcretes, ref abstractTails ) )
                    {
                        _monitor.Warn( $"Service '{c.Type}' is abstract. It is ignored." );
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
                _monitor.Trace( $"Found {_serviceRoots.Count} unambiguous paths." );
                // Initializes all non ambiguous paths.
                for( int i = 0; i < _serviceRoots.Count; ++i )
                {
                    var leaf = resolvedLeaves[i];
                    if( leaf != null )
                    {
                        // Here, calling leaf.EnsureCtorBinding() would be enough but Service Resolution requires
                        // the closure on all leaves. GetCtorParametersClassClosure calls EnsureCtorBinding.
                        leaf.GetCtorParametersClassClosure( _monitor, this, ref error );
                        error |= !_serviceRoots[i].SetMostSpecialized( _monitor, engineMap, leaf );
                    }
                }
                // Every non ambiguous paths have been initialized.
                // Now, if there is no initialization error, tries to resolve class ambiguities.
                List<IReadOnlyList<AutoServiceClassInfo>> remainingAmbiguities = null;
                if( !error && ambiguities != null )
                {
                    using( _monitor.OpenInfo( $"Trying to resolve {ambiguities.Count} ambiguities." ) )
                    {
                        var resolver = new ClassAmbiguityResolver( _monitor, this, engineMap );
                        foreach( var a in ambiguities )
                        {
                            using( _monitor.OpenTrace( $"Trying to resolve class ambiguities for {a.Root.Type}." ) )
                            {
                                var (success, initError) = resolver.TryClassUnification( a.Root, a.Leaves );
                                error |= initError;
                                if( success )
                                {
                                    _monitor.CloseGroup( "Succeeds, resolved to: " + a.Root.MostSpecialized.Type );
                                }
                                else
                                {
                                    _monitor.CloseGroup( "Failed." );
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

            AutoServiceClassInfo _root;
            AutoServiceClassInfo _rootAmbiguity;
            AutoServiceClassInfo[] _allLeaves;

            readonly struct ClassAmbiguity
            {
                public readonly AutoServiceClassInfo Class;
                public readonly List<AutoServiceClassInfo> Leaves;

                public ClassAmbiguity( AutoServiceClassInfo c )
                {
                    Debug.Assert( c.TypeInfo.SpecializationsCount > 0 && c.MostSpecialized == null );
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

            public (bool Success, bool InitializationError) TryClassUnification( AutoServiceClassInfo root, AutoServiceClassInfo[] allLeaves  )
            {
                Debug.Assert( allLeaves.Length > 1
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
                    Debug.Assert( _root.MostSpecialized == null );
                    Debug.Assert( _rootAmbiguity.MostSpecialized != null );
                    initializationError |= !_root.SetMostSpecialized( _monitor, _engineMap, _rootAmbiguity.MostSpecialized );
                }
                return (allSuccess,initializationError);
            }

            public void CollectRemainingAmbiguities( List<IReadOnlyList<AutoServiceClassInfo>> ambiguities )
            {
                Debug.Assert( _ambiguities.Count > 0 );
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
                            _monitor.Error( $"Class Unification ambiguity: '{a.Type}' is already resolved by '{a.MostSpecialized.Type}'. It can not be resolved also by '{leaf.Type}'." );
                            thisPathIsResolved = false;
                        }
                        else
                        {
                            _monitor.Trace( $"Class Unification: '{a.Type}' resolved to '{leaf.Type}'." );
                            initializationError |= !a.SetMostSpecialized( _monitor, _engineMap, leaf );
                        }
                    }
                    else
                    {
                        thisPathIsResolved = false;
                    }
#if DEBUG
                    // If this leaf worked, it must be the very first one: subsequent ones must fail.
                    Debug.Assert( !thisPathIsResolved || ++resolvedPathCount == 1 );
#endif
                    success |= thisPathIsResolved;
                }
                if( !success )
                {
                    _monitor.Error( $"Service Class Unification: unable to resolve '{a.Type}' to a unique specialization." );
                }
                return (success, initializationError);
            }

            static AutoServiceClassInfo NextUpperAmbiguity( AutoServiceClassInfo start )
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


}
