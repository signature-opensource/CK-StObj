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
    public partial class StObjEngine
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
        public bool Run() => DoRun( null );

        /// <summary>
        /// Runs the setup, delegating the obtention of the <see cref="StObjCollectorResult"/> to an external resolver.
        /// </summary>
        /// <param name="resolver">The resolver to use.</param>
        /// <returns>True on success, false if an error occurred.</returns>
        public bool Run( IStObjCollectorResultResolver resolver )
        {
            if( resolver == null ) throw new ArgumentNullException( nameof( resolver ) );
            return DoRun( resolver );
        }

        bool DoRun( IStObjCollectorResultResolver? resolver )
        {
            if( _startContext != null ) throw new InvalidOperationException( "Run can be called only once." );
            if( !PrepareAndCheckConfigurations() ) return false;
            if( _ckSetupConfig != null && !ApplyCKSetupConfiguration() ) return false;
            var unifiedBinPath = CreateUnifiedBinPathConfiguration( _monitor, _config.BinPaths, _config.GlobalExcludedTypes );
            if( unifiedBinPath == null ) return false;
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
                        StObjCollectorResult? firstRun = null;
                        if( resolver == null )
                        {
                            if( unifiedBinPath.Assemblies.Count == 0 )
                            {
                                _monitor.Error( "No Assemblies specified. Executing a setup with no content is an error." );
                                return _status.Success = false;
                            }
                            firstRun = SafeBuildStObj( unifiedBinPath );
                        }
                        else
                        {
                            firstRun = resolver.GetUnifiedResult( unifiedBinPath );
                        }
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
                            StObjCollectorResult? rFolder = resolver != null ? resolver.GetSecondaryResult( g.Key, g ) : SafeBuildStObj( g.Key );
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
                        // This is where all aspects runs before Code generation: this is where CK.Setupable.Engine.SetupableAspect.Run():
                        // - Builds the ISetupItem items (that support 3 steps setup) associated to each StObj (relies on EngineMap.StObjs.OrderedStObjs).
                        // - Projects the StObj topological order on the ISetupItem items graph.
                        // - Calls the DynamicItemInitialize methods that can create new Setup items (typically as child of existing containers like SqlProcedure on SqlTable)
                        // - The ISetupItems are then sorted topologically (this is the second graph).
                        // - The Init/Install/Settle steps are executed.
                        runCtx.RunAspects( () => _status.Success = false, false );
                    }
                    // Code Generation.
                    if( _status.Success )
                    {
                        string dllName = _config.GeneratedAssemblyName;
                        if( !dllName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) dllName += ".dll";
                        using( _monitor.OpenInfo( "Final Code Generation." ) )
                        {
                            var secondPass = new (StObjEngineRunContext.GenBinPath GenContext, List<MultiPassCodeGeneration> SecondPasses)[runCtx.AllBinPaths.Count];
                            int i = 0;
                            foreach( var g in runCtx.AllBinPaths )
                            {
                                var second = new List<MultiPassCodeGeneration>();
                                secondPass[i++] = (g, second);
                                if( !g.Result.GenerateSourceCodeFirstPass( _monitor, g, _config.InformationalVersion, second ) )
                                {
                                    _status.Success = false;
                                    break;
                                }
                            }
                            if( _status.Success )
                            {
                                Func<IActivityMonitor, SHA1Value, bool> stObjMapAvailable = _config.AvailableStObjMapSignatures.Count > 0
                                                                            ? (m,v) => _config.AvailableStObjMapSignatures.Contains( v )
                                                                            : (m,v) => StObjContextRoot.GetMapInfo( v, m ) != null;
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
                    if( _status.Success )
                    {
                        runCtx.RunAspects( () => _status.Success = false, true );
                    }
                    // Post Code Generation.
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

                var foundAspects = _config.Aspects.Select( r => b.GetAspectConfiguration( r.GetType() ) ).Where( c => c != null ).Select( c => c! );
                var aliens = b.AspectConfigurations.Except( foundAspects );
                if( aliens.Any() )
                {
                    _monitor.Error( $"BinPath configuration {b.Name} contains elements whose name cannot be mapped to any existing aspect: {aliens.Select( a => a.Name.ToString() ).Concatenate()}. Available aspects are: {_config.Aspects.Select( a => a.GetType().Name ).Concatenate()}." );
                    return false;
                }
                foreach( var a in foundAspects )
                {
                    EvalKnownPaths( _monitor, b.Name, a.Name.LocalName, a, _config.BasePath, b.OutputPath, b.ProjectPath );
                }
            }
            // This must be done after the loop above (Name is set when empty).
            if( _config.BinPaths.GroupBy( c => c.Name ).Any( g => g.Count() > 1 ) )
            {
                _monitor.Error( $"BinPath configuration 'Name' must be unique. Duplicates found: {_config.BinPaths.GroupBy( c => c.Name ).Where( g => g.Count() > 1 ).Select( g => g.Key ).Concatenate()}" );
                return false;
            }
            return true;

            static void EvalKnownPaths( IActivityMonitor monitor, string binPathName, string aspectName, XElement element, NormalizedPath basePath, NormalizedPath outputPath, NormalizedPath projectPath )
            {
                foreach( var e in element.Elements() )
                {
                    if( !e.HasElements )
                    {
                        Debug.Assert( Math.Min( Math.Min( "{BasePath}".Length, "{OutputPath}".Length ), "{ProjectPath}".Length ) == 10 );
                        string? v = e.Value;
                        if( v != null && v.Length >= 10 )
                        {
                            var vS = ReplacePattern( basePath, "{BasePath}", v );
                            vS = ReplacePattern( outputPath, "{OutputPath}", vS );
                            vS = ReplacePattern( projectPath, "{ProjectPath}", vS );
                            if( v != vS )
                            {
                                monitor.Trace( $"BinPathConfiguration '{binPathName}', aspect '{aspectName}': Configuration value '{v}' has been evaluated to '{vS}'." );
                                e.Value = vS;
                            }
                        }
                    }
                    else
                    {
                        EvalKnownPaths( monitor, binPathName, aspectName, e, basePath, outputPath, projectPath );
                    }
                }

                static string ReplacePattern( NormalizedPath basePath, string pattern, string v )
                {
                    int len = pattern.Length;
                    if( v.Length >= len )
                    {
                        NormalizedPath result;
                        if( v.StartsWith( pattern, StringComparison.OrdinalIgnoreCase ) )
                        {
                            if( v.Length > len && (v[len] == '\\' || v[len] == '/') ) ++len;
                            result = basePath.Combine( v.Substring( len ) ).ResolveDots();
                        }
                    }
                    return v;
                }
            }
        }

        NormalizedPath MakeAbsolutePath( NormalizedPath p )
        {
            if( !p.IsRooted ) p = _config.BasePath.Combine( p );
            p = p.ResolveDots();
            return p;
        }

        bool ApplyCKSetupConfiguration()
        {
            Debug.Assert( _ckSetupConfig != null );
            using( _monitor.OpenInfo( "Applying CKSetup configuration." ) )
            {
                var binPaths = _ckSetupConfig.Elements( StObjEngineConfiguration.xBinPaths ).SingleOrDefault();
                if( binPaths == null ) throw new ArgumentException( $"Missing &lt;BinPaths&gt; single element in '{_ckSetupConfig}'." );
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

        /// <summary>
        /// Creates a <see cref="BinPathConfiguration"/> that unifies multiple <see cref="BinPathConfiguration"/>.
        /// This configuration is the one used on the unified working directory.
        /// This unified configuration doesn't contain any <see cref="BinPathConfiguration.AspectConfigurations"/>.
        /// </summary>
        /// <param name="monitor">Monitor for error.</param>
        /// <param name="configurations">Multiple configurations.</param>
        /// <param name="globalExcludedTypes">Optional types to exclude: see <see cref="StObjEngineConfiguration.GlobalExcludedTypes"/>.</param>
        /// <returns>The unified configuration or null on error.</returns>
        static BinPathConfiguration? CreateUnifiedBinPathConfiguration( IActivityMonitor monitor, IEnumerable<BinPathConfiguration> configurations, IEnumerable<string>? globalExcludedTypes = null )
        {
            var rootBinPath = new BinPathConfiguration();
            rootBinPath.Path = rootBinPath.OutputPath = AppContext.BaseDirectory;
            // The root (the Working directory) doesn't want any output by itself.
            rootBinPath.GenerateSourceFiles = false;
            Debug.Assert( rootBinPath.CompileOption == CompileOption.None );
            // Assemblies and types are the union of the assemblies and types of the bin paths.
            rootBinPath.Assemblies.AddRange( configurations.SelectMany( b => b.Assemblies ) );

            var fusion = new Dictionary<string, BinPathConfiguration.TypeConfiguration>();
            foreach( var c in configurations.SelectMany( b => b.Types ) )
            {
                if( fusion.TryGetValue( c.Name, out var exists ) )
                {
                    if( !c.Optional ) exists.Optional = false;
                    if( exists.Kind != c.Kind )
                    {
                        monitor.Error( $"Invalid Type configuration accross BinPaths for '{c.Name}': {exists.Kind} vs. {c.Kind}." );
                        return null;
                    }
                }
                else fusion.Add( c.Name, new BinPathConfiguration.TypeConfiguration( c.Name, c.Kind, c.Optional ) );
            }
            rootBinPath.Types.AddRange( fusion.Values );

            // Propagates root excluded types to all bin paths.
            if( globalExcludedTypes != null )
            {
                rootBinPath.ExcludedTypes.AddRange( globalExcludedTypes );
                foreach( var f in configurations ) f.ExcludedTypes.AddRange( rootBinPath.ExcludedTypes );
            }
            return rootBinPath;
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
                // This FullName is also null for (at least) classes nested into nested generic classes.
                // In all cases, we emit a warn and fiters this beast out.
                if( t.FullName == null )
                {
                    monitor.Warn( $"Type has no FullName: '{t.Name}'. It is excluded." );
                    return false;
                }
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
                    _config.BinPaths.Select( b => b.Name! ) );
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
