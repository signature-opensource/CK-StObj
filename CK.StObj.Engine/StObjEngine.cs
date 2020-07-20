using System;
using System.Collections.Generic;
using System.Linq;
using CK.Core;
using System.Diagnostics;
using System.Xml.Linq;
using System.IO;
using CK.Text;

#nullable enable

namespace CK.Setup
{
    /// <summary>
    /// Generic engine that runs a <see cref="StObjEngineConfiguration"/>.
    /// </summary>
    public class StObjEngine
    {
        readonly IActivityMonitor _monitor;
        readonly StObjEngineConfiguration _config;
        readonly XElement? _ckSetupConfig;

        Status? _status;
        StObjEngineConfigureContext? _startContext;

        class Status : IStObjEngineStatus, IDisposable
        {
            readonly IActivityMonitor _m;
            readonly ActivityMonitorPathCatcher _pathCatcher;
            public bool Success;

            public Status( IActivityMonitor m )
            {
                _m = m;
                _pathCatcher = new ActivityMonitorPathCatcher() { IsLocked = true };
                _m.Output.RegisterClient( _pathCatcher );
                Success = true;
            }

            bool IStObjEngineStatus.Success => Success;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement> DynamicPath => _pathCatcher.DynamicPath;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement> LastErrorPath => _pathCatcher.LastErrorPath;

            public IReadOnlyList<ActivityMonitorPathCatcher.PathElement> LastWarnOrErrorPath => _pathCatcher.LastErrorPath;

            public void Dispose()
            {
                _pathCatcher.IsLocked = false;
                _m.Output.UnregisterClient( _pathCatcher );
            }
        }

        /// <summary>
        /// Initializes a new <see cref="StObjEngine"/>.
        /// </summary>
        /// <param name="monitor">Logger that must be used.</param>
        /// <param name="config">Configuration that describes the key aspects of the build.</param>
        public StObjEngine( IActivityMonitor monitor, StObjEngineConfiguration config )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            _monitor = monitor;
            _config = config;
        }

        /// <summary>
        /// Initializes a new <see cref="StObjEngine"/> from a xml element (see <see cref="StObjEngineConfiguration(XElement)"/>).
        /// </summary>
        /// <param name="monitor">Logger that must be used.</param>
        /// <param name="config">Configuration that describes the key aspects of the build.</param>
        public StObjEngine( IActivityMonitor monitor, XElement config )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            _monitor = monitor;
            _config = new StObjEngineConfiguration( config );
            // We are coming from CKSetup: the configuation element has a Engine attribute.
            if( config.Attribute( "Engine" ) != null ) _ckSetupConfig = config;
        }

        /// <summary>
        /// Gets whether this engine is running or has <see cref="Run"/> (it can run only once).
        /// </summary>
        public bool Started => _startContext != null;

        class BinPathComparer : IEqualityComparer<BinPathConfiguration>
        {
            public static BinPathComparer Default = new BinPathComparer();

            public bool Equals( BinPathConfiguration? x, BinPathConfiguration? y )
            {
                Debug.Assert( x != null && y != null );
                bool s = x.Types.Count == y.Types.Count
                         && x.Assemblies.SetEquals( y.Assemblies )
                         && x.ExcludedTypes.SetEquals( y.ExcludedTypes );
                if( s )
                {
                    var one = x.Types.Select( xB => xB.ToString() ).OrderBy( Util.FuncIdentity );
                    var two = x.Types.Select( xB => xB.ToString() ).OrderBy( Util.FuncIdentity );
                    s = one.SequenceEqual( two );
                }
                return s;
            }

            public int GetHashCode( BinPathConfiguration b ) => b.ExcludedTypes.Count
                                                    + b.Types.Count * 59
                                                    + b.Assemblies.Count * 117;
        }

        /// <summary>
        /// Runs the setup.
        /// </summary>
        /// <returns>True on success, false if an error occurred.</returns>
        public bool Run()
        {
            if( _startContext != null ) throw new InvalidOperationException( "Run can be called only once." );
            if( !PrepareAndCheckConfigurations() ) return false;
            if( _ckSetupConfig != null && !ApplyCKSetupConfiguration() ) return false;
            var unifiedBinPath = BinPathConfiguration.CreateUnified( _monitor, _config.BinPaths, _config.GlobalExcludedTypes );
            if( unifiedBinPath == null ) return false;
            if( unifiedBinPath.Assemblies.Count == 0 )
            {
                _monitor.Error( "No Assemblies specified. Executing a setup with no content is an error." );
                return false;
            }
            // Groups similar configurations to optimize runs.
            var groups = _config.BinPaths.Append( unifiedBinPath ).GroupBy( Util.FuncIdentity, BinPathComparer.Default ).ToList();
            var rootGroup = groups.Single( g => g.Contains( unifiedBinPath ) );

            _status = new Status( _monitor );
            _startContext = new StObjEngineConfigureContext( _monitor, _config, _status );
            try
            {
                _startContext.CreateAndConfigureAspects( _config.Aspects, () => _status.Success = false );
                if( _status.Success )
                {
                    StObjEngineRunContext runCtx;
                    using( _monitor.OpenInfo( "Creating unified map." ) )
                    {
                        StObjCollectorResult? firstRun = SafeBuildStObj( unifiedBinPath );
                        if( firstRun == null ) return _status.Success = false;
                        // Primary StObjMap has been successfully built, we can initialize the run context
                        // with this primary StObjMap and the bin paths that use it (including the unifiedBinPath).
                        runCtx = new StObjEngineRunContext( _monitor, _startContext, rootGroup, firstRun );
                    }

                    // Then for each set of compatible BinPaths, we can create the secondaries StObjMaps.
                    foreach( var g in groups.Where( g => g != rootGroup ) )
                    {
                        using( _monitor.OpenInfo( $"Creating secondary map for BinPaths '{g.Select( b => b.Path.Path ).Concatenate( "', '" )}'." ) )
                        {
                            StObjCollectorResult? rFolder = SafeBuildStObj( g.Key );
                            if( rFolder == null )
                            {
                                _status.Success = false;
                                break;
                            }
                            runCtx.AddResult( g, rFolder );
                        }
                    }
                    if( _status.Success )
                    {
                        // This is where all aspects runs: this where CK.Setupable.Engine.SetupableAspect.Run():
                        // - Builds the ISetupItem items (that support 3 steps setup) associated to each StObj (relies on EngineMap.StObjs.OrderedStObjs).
                        // - Projects the StObj topological order on the ISetupItem items graph.
                        // - Calls the DynamicItemInitialize methods that can create new Setup items (typically as child of existing containers like SqlProcedure on SqlTable)
                        // - The ISetupItems are then sorted topologically (this is the second graph).
                        // - The Init/Install/Settle steps are executed.
                        runCtx.RunAspects( () => _status.Success = false );
                    }
                    if( _status.Success )
                    {
                        string dllName = _config.GeneratedAssemblyName;
                        if( !dllName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) dllName += ".dll";
                        using( _monitor.OpenInfo( "Final Code Generation." ) )
                        {
                            var secondPass = new (StObjEngineRunContext.GenBinPath GenContext, List<SecondPassCodeGeneration> SecondPasses)[runCtx.AllBinPaths.Count];
                            int i = 0;
                            foreach( var g in runCtx.AllBinPaths )
                            {
                                var second = new List<SecondPassCodeGeneration>();
                                secondPass[i] = (g, second);
                                if( !g.Result.GenerateSourceCodeFirstPass( _monitor, g, _config.InformationalVersion, second.Add ) )
                                {
                                    _status.Success = false;
                                    break;
                                }
                            }
                            if( _status.Success )
                            {
                                Func<SHA1Value, bool> stObjMapAvailable = _config.AvailableStObjMapSignatures.Count > 0
                                                                            ? _config.AvailableStObjMapSignatures.Contains
                                                                            : (Func<SHA1Value, bool>)(v => StObjContextRoot.GetMapInfo( v, _monitor ) != null);
                                foreach( var (g, secondPasses) in secondPass )
                                {
                                    var head = g.GroupedPaths.Key;
                                    var primaryOutputFile = head.OutputPath.AppendPart( dllName );
                                    StObjCollectorResult.CodeGenerateResult gR = g.Result.GenerateSourceCodeSecondPass( _monitor, primaryOutputFile, g, secondPasses, stObjMapAvailable );
                                    if( gR.GeneratedFileNames.Count > 0 )
                                    {
                                        foreach( var f in g.GroupedPaths )
                                        {
                                            if( !f.GenerateSourceFiles && f.CompileOption == CompileOption.None ) continue;

                                            NormalizedPath outPath = head.OutputPath;
                                            // Handling OutputPath: if the OutputPath is not empty and is not already the primary one,
                                            // We move all the generated files.
                                            if( !f.OutputPath.IsEmptyPath
                                                && f.OutputPath != head.OutputPath )
                                            {
                                                outPath = f.OutputPath;
                                                foreach( var file in gR.GeneratedFileNames )
                                                {
                                                    ProjectSourceFileHandler.DoMoveOrCopy( _monitor,
                                                                                     head.OutputPath.Combine( file ),
                                                                                     outPath.Combine( file ),
                                                                                     copy: file.EndsWith( ".dll" ) );
                                                }
                                            }
                                            // Once done, if there is a ProjectPath that is not the OutputPath, then
                                            // we handle the "Project Mode" source files.
                                            // There are 2 moves for file sources but the code is simpler.
                                            if( !f.ProjectPath.IsEmptyPath
                                                && f.ProjectPath != outPath )
                                            {
                                                var h = new ProjectSourceFileHandler( _monitor, outPath, f.ProjectPath );
                                                h.MoveFilesAndCheckSignature( gR );
                                            }
                                        }
                                    }
                                    if( !gR.Success )
                                    {
                                        _status.Success = false;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if( !_status.Success )
                    {
                        var errorPath = _status.LastErrorPath;
                        if( errorPath == null || errorPath.Count == 0 )
                        {
                            _monitor.Fatal( "Success status is false but no error has been logged." );
                        }
                        else
                        {
                            _monitor.Error( errorPath.ToStringPath() );
                        }
                    }
                    var termCtx = new StObjEngineTerminateContext( _monitor, runCtx );
                    termCtx.TerminateAspects( () => _status.Success = false );
                }
                return _status.Success;
            }
            finally
            {
                DisposeDisposableAspects();
                _status.Dispose();
            }
        }

        /// <summary>
        /// Ensures that <see cref="BinPathConfiguration.Path"/>, <see cref="BinPathConfiguration.OutputPath"/>
        /// are rooted and gives automatic numbered names to empty <see cref="BinPathConfiguration.Name"/>.
        /// </summary>
        /// <returns>True on success, false is something's wrong.</returns>
        bool PrepareAndCheckConfigurations()
        {
            if( _config.BinPaths.Count == 0 )
            {
                _monitor.Error( $"No BinPath defined in the configuration. Nothing can be processed." );
                return false;
            }
            if( _config.BasePath.IsEmptyPath )
            {
                _config.BasePath = Environment.CurrentDirectory;
                _monitor.Info( $"No BasePath. Using current directory '{_config.BasePath}'." );
            }
            int idx = 1;
            foreach( var b in _config.BinPaths )
            {
                b.Path = MakeAbsolutePath( b.Path );

                if( b.OutputPath.IsEmptyPath ) b.OutputPath = b.Path;
                else b.OutputPath = MakeAbsolutePath( b.OutputPath );

                if( !b.ProjectPath.IsEmptyPath ) b.ProjectPath = MakeAbsolutePath( b.ProjectPath );

                if( String.IsNullOrWhiteSpace( b.Name ) ) b.Name = $"BinPath{idx}";
                ++idx;
            }
            if( _config.BinPaths.GroupBy( c => c.Name ).Any( g => g.Count() > 1 ) )
            {
                _monitor.Error( $"BinPath configuration names must be unique. Duplicates: {_config.BinPaths.GroupBy( c => c.Name ).Where( g => g.Count() > 1 ).Select( g => g.Key ).Concatenate()}" );
                return false;
            }
            return true;
        }

        NormalizedPath MakeAbsolutePath( NormalizedPath pp )
        {
            if( !pp.IsRooted ) pp = _config.BasePath.Combine( pp );
            pp = pp.ResolveDots();
            return pp;
        }

        bool ApplyCKSetupConfiguration()
        {
            Debug.Assert( _ckSetupConfig != null );
            using( _monitor.OpenInfo( "Applying CKSetup configuration." ) )
            {
                var binPaths = _ckSetupConfig.Elements( StObjEngineConfiguration.xBinPaths ).SingleOrDefault();
                if( binPaths == null ) throw new ArgumentException( $"Missing &lt;BinPaths&gt; element in '{_ckSetupConfig}'." );
                foreach( XElement xB in binPaths.Elements( StObjEngineConfiguration.xBinPath ) )
                {
                    var assemblies = xB.Descendants()
                                       .Where( e => e.Name == "Model" || e.Name == "ModelDependent" )
                                       .Select( e => e.Value )
                                       .Where( s => s != null );

                    var path = (string)xB.Attribute( StObjEngineConfiguration.xPath );
                    if( path == null ) throw new ArgumentException( $"Missing Path attribute in '{xB}'." );

                    var rootedPath = MakeAbsolutePath( path );
                    var c = _config.BinPaths.SingleOrDefault( b => b.Path == rootedPath );
                    if( c == null ) throw new ArgumentException( $"Unable to find one BinPath element with Path '{rootedPath}' in: {_config.ToXml()}." );

                    c.Assemblies.AddRange( assemblies );
                    _monitor.Info( $"Added assemblies from CKSetup to BinPath '{rootedPath}':{Environment.NewLine}{assemblies.Concatenate(Environment.NewLine)}." );
                }
                return true;
            }
        }

        class TypeFilterFromConfiguration : IStObjTypeFilter
        {
            readonly StObjConfigurationLayer? _firstLayer;
            readonly HashSet<string> _excludedTypes;

            public TypeFilterFromConfiguration( BinPathConfiguration f, StObjConfigurationLayer? firstLayer )
            {
                _excludedTypes = f.ExcludedTypes;
                _firstLayer = firstLayer;
            }

            bool IStObjTypeFilter.TypeFilter( IActivityMonitor monitor, Type t )
            {
                // Type.FullName is null if the current instance represents a generic type parameter, an array
                // type, pointer type, or byref type based on a type parameter, or a generic type
                // that is not a generic type definition but contains unresolved type parameters.
                if( t.FullName == null ) throw new ArgumentException( "Invalid type", nameof( t ) );
                Debug.Assert( t.AssemblyQualifiedName != null, "Since FullName is defined." );
                if( _excludedTypes.Contains( t.Name ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type Name." );
                    return false;
                }
                if( _excludedTypes.Contains( t.FullName ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type FullName." );
                    return false;
                }
                if( _excludedTypes.Contains( t.AssemblyQualifiedName ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its Type AssemblyQualifiedName." );
                    return false;
                }
                if( SimpleTypeFinder.WeakenAssemblyQualifiedName( t.AssemblyQualifiedName, out var weaken )
                    && _excludedTypes.Contains( weaken ) )
                {
                    monitor.Info( $"Type {t.AssemblyQualifiedName} is filtered out by its weak type name ({weaken})." );
                    return false;
                }
                return _firstLayer?.TypeFilter( monitor, t ) ?? true;
            }
        }

        StObjCollectorResult? SafeBuildStObj( BinPathConfiguration head )
        {
            Debug.Assert( _startContext != null, "Work started." );
            bool hasError = false;
            using( _monitor.OnError( () => hasError = true ) )
            {
                StObjCollectorResult result;
                var configurator = _startContext.Configurator.FirstLayer;
                var typeFilter = new TypeFilterFromConfiguration( head, configurator );
                StObjCollector stObjC = new StObjCollector(
                    _monitor,
                    _startContext.ServiceContainer,
                    _config.TraceDependencySorterInput,
                    _config.TraceDependencySorterOutput,
                    typeFilter, configurator, configurator,
                    _config.BinPaths.Select( b => b.Name ) );
                stObjC.RevertOrderingNames = _config.RevertOrderingNames;
                using( _monitor.OpenInfo( "Registering types." ) )
                {
                    // First handles the explicit kind of Types.
                    foreach( var c in head.Types )
                    {
                        // When c.Kind is None, !Optional is challenged.
                        // The Type is always resolved.
                        stObjC.SetAutoServiceKind( c.Name, c.Kind, c.Optional );
                    }
                    // Then registers the types from the assemblies.
                    stObjC.RegisterAssemblyTypes( head.Assemblies );
                    // Explicitly registers the non optional Types.
                    stObjC.RegisterTypes( head.Types.Where( c => c.Optional == false ).Select( c => c.Name ).ToList() );
                    // Finally, registers the code based explicitly registered types.
                    foreach( var t in _startContext.ExplicitRegisteredTypes ) stObjC.RegisterType( t );

                    Debug.Assert( stObjC.RegisteringFatalOrErrorCount == 0 || hasError, "stObjC.RegisteringFatalOrErrorCount > 0 ==> An error has been logged." );
                }
                if( stObjC.RegisteringFatalOrErrorCount == 0 )
                {
                    using( _monitor.OpenInfo( "Resolving Real Objects & AutoService dependency graph." ) )
                    {
                        result = stObjC.GetResult();
                        Debug.Assert( !result.HasFatalError || hasError, "result.HasFatalError ==> An error has been logged." );
                    }
                    if( !result.HasFatalError ) return result;
                }
            }
            return null;
        }

        /// <summary>
        /// Disposes all disposable aspects.
        /// </summary>
        void DisposeDisposableAspects()
        {
            Debug.Assert( _startContext != null, "Work started." );
            foreach( var aspect in _startContext.Aspects.OfType<IDisposable>() )
            {
                try
                {
                    aspect.Dispose();
                }
                catch( Exception ex )
                {
                    _monitor.Error( $"While disposing Aspect '{aspect.GetType().AssemblyQualifiedName}'.", ex );
                }
            }
        }

    }
}
