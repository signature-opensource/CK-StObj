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
        readonly IStObjRuntimeBuilder _runtimeBuilder;


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
        /// <param name="runtimeBuilder">The object in charge of actual objects instantiation. When null, <see cref="StObjContextRoot.DefaultStObjRuntimeBuilder"/> is used.</param>
        public StObjEngine( IActivityMonitor monitor, StObjEngineConfiguration config, IStObjRuntimeBuilder? runtimeBuilder = null )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( config == null ) throw new ArgumentNullException( nameof( config ) );
            _monitor = monitor;
            _config = config;
            _runtimeBuilder = runtimeBuilder ?? StObjContextRoot.DefaultStObjRuntimeBuilder;
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
            _runtimeBuilder = StObjContextRoot.DefaultStObjRuntimeBuilder;
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

            public bool Equals( BinPathConfiguration x, BinPathConfiguration y )
            {
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
            var unifiedBinPath = CreateUnifiedBinPathFromAllBinPaths();
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
                    StObjCollectorResult? firstRun = SafeBuildStObj( unifiedBinPath, null );
                    if( firstRun == null ) return _status.Success = false;

                    // Primary StObjMap has been successfully built, we can initialize the run context
                    // with this primary StObjMap and the bin paths that use it (including the unifiedBinPath).
                    var runCtx = new StObjEngineRunContext( _monitor, _startContext, rootGroup, firstRun );

                    // Then for each set of compatible BinPaths, we can create the secondaries StObjMaps.
                    foreach( var g in groups.Where( g => g != rootGroup ) )
                    {
                        using( _monitor.OpenInfo( $"Creating secondary map for BinPaths '{g.Select( b => b.Path.Path ).Concatenate( "', '" )}'." ) )
                        {
                            StObjCollectorResult? rFolder = SafeBuildStObj( g.Key, firstRun.SecondaryRunAccessor );
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
                        runCtx.RunAspects( () => _status.Success = false );
                    }
                    if( _status.Success )
                    {
                        string dllName = _config.GeneratedAssemblyName;
                        if( !dllName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) dllName += ".dll";
                        using( _monitor.OpenInfo( "Final Code Generation." ) )
                        {
                            using( _monitor.OpenInfo( "Generating AppContext assembly (first run)." ) )
                            {
                                // Use the rootgroup here: the Key of the group is the unified path and will be used
                                // as the initial target for files.
                                _status.Success = CodeGenerationForPaths( rootGroup, firstRun, dllName );
                            }
                            if( _status.Success )
                            {
                                foreach( var b in runCtx.AllBinPaths.Skip( 1 ) )
                                {
                                    Debug.Assert( b.GroupedPaths != null, "Secondary runs have paths." );
                                    if( !CodeGenerationForPaths( b.GroupedPaths, b.Result, dllName ) )
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
        /// Ensures thar <see cref="BinPathConfiguration.Path"/>, <see cref="BinPathConfiguration.OutputPath"/>
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

                if( String.IsNullOrWhiteSpace( b.Name ) ) b.Name = $"BinPath#{idx}";
                ++idx;
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

        BinPathConfiguration? CreateUnifiedBinPathFromAllBinPaths()
        {
            var rootBinPath = new BinPathConfiguration();
            rootBinPath.Path = rootBinPath.OutputPath = AppContext.BaseDirectory;
            // The root (the Working directory) doesn't want any output by itself.
            rootBinPath.GenerateSourceFiles = false;
            rootBinPath.SkipCompilation = true;
            // Assemblies and types are the union of the assembblies and types of the bin paths.
            rootBinPath.Assemblies.AddRange( _config.BinPaths.SelectMany( b => b.Assemblies ) );

            var fusion = new Dictionary<string, BinPathConfiguration.TypeConfiguration>();
            foreach( var c in _config.BinPaths.SelectMany( b => b.Types ) )
            {
                if( fusion.TryGetValue( c.Name, out var exists ) )
                {
                    if( !c.Optional ) exists.Optional = false;
                    if( exists.Kind != c.Kind )
                    {
                        _monitor.Error( $"Invalid Type configuration accross BinPaths for '{c.Name}': {exists.Kind} vs. {c.Kind}." );
                        return null;
                    }
                }
                else fusion.Add( c.Name, new BinPathConfiguration.TypeConfiguration( c.Name, c.Kind, c.Optional ) );
            }
            rootBinPath.Types.AddRange( fusion.Values );

            // Propagates root excluded types to all bin paths.
            rootBinPath.ExcludedTypes.AddRange( _config.GlobalExcludedTypes );
            foreach( var f in _config.BinPaths ) f.ExcludedTypes.AddRange( rootBinPath.ExcludedTypes );

            return rootBinPath;
        }


        bool CodeGenerationForPaths( IGrouping<BinPathConfiguration, BinPathConfiguration> bPaths, StObjCollectorResult r, string dllName )
        {
            var head = bPaths.Key;
            var g = r.GenerateFinalAssembly( _monitor, head.OutputPath.AppendPart( dllName ), bPaths.Any( f => f.GenerateSourceFiles ), _config.InformationalVersion, bPaths.All( f => f.SkipCompilation ) );
            if( g.GeneratedFileNames.Count > 0 )
            {
                foreach( var f in bPaths )
                {
                    if( !f.GenerateSourceFiles && f.SkipCompilation ) continue;
                    var dir = f.OutputPath;
                    if( dir == head.OutputPath ) continue;
                    using( _monitor.OpenInfo( $"Copying generated files to folder: '{dir}'." ) )
                    {
                        foreach( var file in g.GeneratedFileNames )
                        {
                            if( file == dllName )
                            {
                                if( f.SkipCompilation ) continue;
                            }
                            else
                            {
                                if( !f.GenerateSourceFiles ) continue;
                            }
                            try
                            {
                                _monitor.Info( file );
                                File.Copy( head.OutputPath.Combine( file ), dir.Combine( file ), true );
                            }
                            catch( Exception ex )
                            {
                                _monitor.Error( ex );
                                return false;
                            }
                        }
                    }
                }
            }
            return g.Success;
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

        StObjCollectorResult? SafeBuildStObj( BinPathConfiguration f, Func<string,object>? secondaryRunAccessor )
        {
            Debug.Assert( _startContext != null, "Work started." );
            bool hasError = false;
            using( _monitor.OnError( () => hasError = true ) )
            using( secondaryRunAccessor == null ? _monitor.OpenInfo( "Building unified Engine map." ) : null )
            {
                StObjCollectorResult result;
                var configurator = _startContext.Configurator.FirstLayer;
                var typeFilter = new TypeFilterFromConfiguration( f, configurator );
                StObjCollector stObjC = new StObjCollector(
                    _monitor,
                    _startContext.ServiceContainer,
                    _config.TraceDependencySorterInput,
                    _config.TraceDependencySorterOutput,
                    _runtimeBuilder,
                    typeFilter, configurator, configurator,
                    secondaryRunAccessor );
                stObjC.RevertOrderingNames = _config.RevertOrderingNames;
                using( _monitor.OpenInfo( "Registering types." ) )
                {
                    // First handles the explicit kind of Types.
                    foreach( var c in f.Types )
                    {
                        // When c.Kind is None, !Optional is challenged.
                        // The Type is always resolved.
                        stObjC.SetAutoServiceKind( c.Name, c.Kind, c.Optional );
                    }
                    // Then registers the types from the assemblies.
                    stObjC.RegisterAssemblyTypes( f.Assemblies );
                    // Explicitly registers the non optional Types.
                    stObjC.RegisterTypes( f.Types.Where( c => c.Optional == false ).Select( c => c.Name ).ToList() );
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
