using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="IRunningEngineConfiguration"/>.
    /// </summary>
    public sealed class RunningEngineConfiguration : IRunningEngineConfiguration
    {
        readonly List<RunningBinPathGroup> _binPathGroups;

        internal RunningEngineConfiguration( EngineConfiguration configuration )
        {
            _binPathGroups = new List<RunningBinPathGroup>();
            Configuration = configuration;
        }

        /// <inheritdoc />
        public EngineConfiguration Configuration { get; }

        /// <inheritdoc cref="IRunningEngineConfiguration.Groups" />
        public IReadOnlyList<RunningBinPathGroup> Groups => _binPathGroups;

        IReadOnlyList<IRunningBinPathGroup> IRunningEngineConfiguration.Groups => _binPathGroups;

        /// <summary>
        /// Ensures that <see cref="BinPathConfiguration.Path"/>, <see cref="BinPathConfiguration.OutputPath"/>
        /// are rooted and gives automatic numbered names to empty <see cref="BinPathConfiguration.Name"/>.
        /// <para>
        /// Any element or attribute value that start with '{BasePath}', '{OutputPath}' and '{ProjectPath}' are evaluated.
        /// </para>
        /// <para>
        /// This method is public to ease tests.
        /// </para>
        /// </summary>
        /// <returns>True on success, false is something's wrong.</returns>
        public static bool CheckAndValidate( IActivityMonitor monitor, EngineConfiguration c )
        {
            if( c.BinPaths.Count == 0 )
            {
                monitor.Error( $"No BinPath defined in the configuration. Nothing can be processed." );
                return false;
            }
            if( c.BasePath.IsEmptyPath )
            {
                c.BasePath = Environment.CurrentDirectory;
                monitor.Info( $"No BasePath. Using current directory '{c.BasePath}'." );
            }
            if( c.GeneratedAssemblyName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) )
            {
                monitor.Info( $"GeneratedAssemblyName should not end with '.dll'. Removing suffix." );
                c.GeneratedAssemblyName += c.GeneratedAssemblyName.Substring( 0, c.GeneratedAssemblyName.Length - 4 );
            }
            int idx = 1;
            foreach( var b in c.BinPaths )
            {
                b.Path = MakeAbsolutePath( c, b.Path );

                if( b.OutputPath.IsEmptyPath ) b.OutputPath = b.Path;
                else b.OutputPath = MakeAbsolutePath( c, b.OutputPath );

                if( b.ProjectPath.IsEmptyPath ) b.ProjectPath = b.OutputPath;
                else
                {
                    b.ProjectPath = MakeAbsolutePath( c, b.ProjectPath );
                    if( b.ProjectPath.LastPart != "$StObjGen" )
                    {
                        b.ProjectPath = b.ProjectPath.AppendPart( "$StObjGen" );
                    }
                }
                if( String.IsNullOrWhiteSpace( b.Name ) ) b.Name = $"BinPath{idx}";
                ++idx;

                bool hasChanged;
                foreach( var binPathAspect in b.Aspects )
                {
                    hasChanged = false;
                    var e = binPathAspect.ToXml();
                    EvalKnownPaths( monitor, b.Name, binPathAspect.AspectName, e, c.BasePath, b.OutputPath, b.ProjectPath, ref hasChanged );
                    if( hasChanged ) binPathAspect.InitializeFrom( e );
                }
            }
            // This must be done after the loop above (Name is set when empty).
            var byName = c.BinPaths.GroupBy( c => c.Name );
            if( byName.Any( g => g.Count() > 1 ) )
            {
                monitor.Error( $"BinPath configuration 'Name' must be unique. Duplicates found: {byName.Where( g => g.Count() > 1 ).Select( g => g.Key ).Concatenate()}" );
                return false;
            }
            return true;

            static void EvalKnownPaths( IActivityMonitor monitor,
                                        string binPathName,
                                        string aspectName,
                                        XElement element,
                                        NormalizedPath basePath,
                                        NormalizedPath outputPath,
                                        NormalizedPath projectPath,
                                        ref bool hasChanged )
            {
                EvalAttributes( monitor, binPathName, aspectName, basePath, outputPath, projectPath, ref hasChanged, element );
                foreach( var e in element.Elements() )
                {
                    EvalAttributes( monitor, binPathName, aspectName, basePath, outputPath, projectPath, ref hasChanged, e );
                    if( !e.HasElements )
                    {
                        if( EvalString( monitor, binPathName, aspectName, basePath, outputPath, projectPath, e.Value, out string? mapped ) )
                        {
                            e.Value = mapped;
                            hasChanged = true;
                        }
                    }
                    else
                    {
                        EvalKnownPaths( monitor, binPathName, aspectName, e, basePath, outputPath, projectPath, ref hasChanged );
                    }
                }

                static bool EvalString( IActivityMonitor monitor,
                                        string binPathName,
                                        string aspectName,
                                        NormalizedPath basePath,
                                        NormalizedPath outputPath,
                                        NormalizedPath projectPath,
                                        string? v,
                                        [NotNullWhen( true )] out string? mapped )
                {
                    if( v != null && v.Length >= 10 )
                    {
                        Throw.DebugAssert( Math.Min( Math.Min( "{BasePath}".Length, "{OutputPath}".Length ), "{ProjectPath}".Length ) == 10 );
                        var vS = ReplacePattern( basePath, "{BasePath}", v );
                        vS = ReplacePattern( outputPath, "{OutputPath}", vS );
                        vS = ReplacePattern( projectPath, "{ProjectPath}", vS );
                        if( v != vS )
                        {
                            monitor.Trace( $"BinPathConfiguration '{binPathName}', aspect '{aspectName}': Configuration value '{v}' has been evaluated to '{vS}'." );
                            mapped = vS;
                            return true;
                        }
                    }
                    mapped = null;
                    return false;

                    static string ReplacePattern( NormalizedPath basePath, string pattern, string v )
                    {
                        int len = pattern.Length;
                        if( v.Length >= len )
                        {
                            if( v.StartsWith( pattern, StringComparison.OrdinalIgnoreCase ) )
                            {
                                if( v.Length > len && (v[len] == '\\' || v[len] == '/') ) ++len;
                                return basePath.Combine( v.Substring( len ) ).ResolveDots();
                            }
                        }
                        return v;
                    }

                }

                static void EvalAttributes( IActivityMonitor monitor,
                                           string binPathName,
                                           string aspectName,
                                           NormalizedPath basePath,
                                           NormalizedPath outputPath,
                                           NormalizedPath projectPath,
                                           ref bool hasChanged,
                                           XElement e )
                {
                    foreach( var a in e.Attributes() )
                    {
                        if( EvalString( monitor, binPathName, aspectName, basePath, outputPath, projectPath, a.Value, out string? mapped ) )
                        {
                            a.Value = mapped;
                            hasChanged = true;
                        }
                    }
                }
            }
        }

        static NormalizedPath MakeAbsolutePath( EngineConfiguration c, NormalizedPath p )
        {
            if( !p.IsRooted ) p = c.BasePath.Combine( p );
            p = p.ResolveDots();
            return p;
        }

        /// <summary>
        /// This throws if the CKSetup configuration cannot be used for any reason (missing
        /// expected information).
        /// </summary>
        internal void ApplyCKSetupConfiguration( IActivityMonitor monitor, XElement ckSetupConfig )
        {
            Debug.Assert( ckSetupConfig != null );
            using( monitor.OpenInfo( "Applying CKSetup configuration." ) )
            {
                var ckSetupSHA1 = (string?)ckSetupConfig.Element( "RunSignature" );
                if( Configuration.BaseSHA1.IsZero )
                {
                    if( ckSetupSHA1 != null )
                    {
                        Configuration.BaseSHA1 = SHA1Value.Parse( ckSetupSHA1 );
                        monitor.Trace( $"Using CKSetup RunSignature '{Configuration.BaseSHA1}' for BaseSHA1." );
                    }
                }
                else
                {
                    if( ckSetupSHA1 == null )
                    {
                        monitor.Trace( $"Using BaseSHA1 '{Configuration.BaseSHA1}' from engine configuration (no RunSignature from CKSetup)." );
                    }
                    else
                    {
                        monitor.Warn( $"Using BaseSHA1 '{Configuration.BaseSHA1}' from engine configuration, ignoring CKSetup RunSignature '{ckSetupSHA1}'." );
                    }
                }

                var binPaths = ckSetupConfig.Elements( EngineConfiguration.xBinPaths ).SingleOrDefault();
                if( binPaths == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Missing &lt;BinPaths&gt; single element in '{ckSetupConfig}'." );

                foreach( XElement xB in binPaths.Elements( EngineConfiguration.xBinPath ) )
                {
                    var assemblies = xB.Descendants()
                                       .Where( e => e.Name == "Model" || e.Name == "ModelDependent" )
                                       .Select( e => e.Value )
                                       .Where( s => s != null );

                    var path = (string?)xB.Attribute( EngineConfiguration.xPath );
                    if( path == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Missing Path attribute in '{xB}'." );

                    var rootedPath = MakeAbsolutePath( Configuration, path );
                    var c = Configuration.BinPaths.SingleOrDefault( b => b.Path == rootedPath );
                    if( c == null ) Throw.ArgumentException( nameof( ckSetupConfig ), $"Unable to find one BinPath element with Path '{rootedPath}' in: {Configuration.ToXml()}." );

                    c.Assemblies.AddRange( assemblies );
                    monitor.Info( $"Added assemblies from CKSetup to BinPath '{rootedPath}':{Environment.NewLine}{assemblies.Concatenate( Environment.NewLine )}." );
                }
            }
        }

        internal bool Initialize( IActivityMonitor monitor, out bool canSkipRun )
        {
            // Lets be optimistic (and if an error occurred the returned false will skip the run anyway).
            // If ForceRun is true, we'll always run. This flag can only transition from true to false.
            canSkipRun = !Configuration.ForceRun;
            if( Configuration.BaseSHA1.IsZero || Configuration.BaseSHA1 == SHA1Value.Empty )
            {
                Configuration.BaseSHA1 = SHA1Value.Zero;
                monitor.Info( $"Zero or Empty BaseSHA1, the generated code source SHA1 will be used." );
            }
            if( Configuration.BinPaths.Count == 1 )
            {
                var b = Configuration.BinPaths[0];
                b.ExcludedTypes.AddRange( Configuration.GlobalExcludedTypes );
                _binPathGroups.Add( new RunningBinPathGroup( Configuration, b, Configuration.BaseSHA1 ) );
                monitor.Trace( $"No unification required (single BinPath)." );
            }
            else
            {
                if( !InitializeMultipleBinPaths( monitor ) ) return false;
            }
            // Provides the canSkipRun to each group.
            foreach( var g in _binPathGroups )
            {
                if( !g.Initialize( monitor, Configuration.ForceRun, ref canSkipRun ) ) return false;
            }
            return true;
        }

        bool InitializeMultipleBinPaths( IActivityMonitor monitor )
        {
            // Propagates root excluded types to all bin paths.
            foreach( var f in Configuration.BinPaths )
            {
                f.ExcludedTypes.AddRange( Configuration.GlobalExcludedTypes );
            }

            // Starts by grouping actual BinPaths by similarity.
            foreach( var g in Configuration.BinPaths.GroupBy( Util.FuncIdentity, SimilarBinPathComparer.Default ) )
            {
                // Ordering the set here to ensure a deterministic head for the group. 
                var similar = g.OrderBy( b => b.Path ).ToArray();
                var shaGroup = Configuration.BaseSHA1.IsZero
                                ? SHA1Value.Zero
                                : SHA1Value.ComputeHash( Configuration.BaseSHA1.ToString() + similar[0].Path );
                var group = new RunningBinPathGroup( Configuration, similar[0], similar, shaGroup );
                _binPathGroups.Add( group );
            }
            if( _binPathGroups.Count > 1 )
            {
                // Create the unified BinPath. If this fails, it's useless to continue.
                var unifiedBinPath = CreateUnifiedBinPathConfiguration( monitor, _binPathGroups, Configuration.GlobalExcludedTypes );
                if( unifiedBinPath == null ) return false;

                // Is the UnifiedBinPath required?
                int idx = _binPathGroups.FindIndex( b => SimilarBinPathComparer.Default.Equals( b.Configuration, unifiedBinPath ) );
                RunningBinPathGroup primaryRun;
                if( idx >= 0 )
                {
                    primaryRun = _binPathGroups[idx];
                    monitor.Trace( $"No unification required, BinPath '{primaryRun.Configuration.Path}' covers all the required components." );
                    _binPathGroups.RemoveAt( idx );
                }
                else
                {
                    primaryRun = new RunningBinPathGroup( Configuration, unifiedBinPath );
                    monitor.Info( $"Unified group is required." );
                }
                // Ensures that it is the first in the list of BinPaths to process:
                // the "covering" bin path must be the PrimaryPath, be it a pure unified or a regular one.
                _binPathGroups.Insert( 0, primaryRun );
            }
            else
            {
                monitor.Trace( $"No unification required, {Configuration.BinPaths.Count} BinPaths share the same components." );
            }
            return true;
        }

        /// <summary>
        /// Considers two BinPaths to be equal if and only if they have the same Assemblies,
        /// same ExcludedTypes and Types configurations.
        /// </summary>
        sealed class SimilarBinPathComparer : IEqualityComparer<BinPathConfiguration>
        {
            public static SimilarBinPathComparer Default = new SimilarBinPathComparer();

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
        /// Creates a <see cref="BinPathConfiguration"/> that unifies multiple <see cref="BinPathConfiguration"/>.
        /// This configuration is the one used on the unified working directory.
        /// This unified configuration doesn't contain any <see cref="BinPathConfiguration.Aspects"/>.
        /// </summary>
        /// <param name="monitor">Monitor for error.</param>
        /// <param name="configurations">Multiple configurations.</param>
        /// <param name="globalExcludedTypes">Types to exclude: see <see cref="EngineConfiguration.GlobalExcludedTypes"/>.</param>
        /// <returns>The unified configuration or null on error.</returns>
        static BinPathConfiguration? CreateUnifiedBinPathConfiguration( IActivityMonitor monitor,
                                                                        IEnumerable<RunningBinPathGroup> configurations,
                                                                        IEnumerable<string> globalExcludedTypes )
        {
            var unified = new BinPathConfiguration()
            {
                Path = AppContext.BaseDirectory,
                OutputPath = AppContext.BaseDirectory,
                Name = "(Unified)",
                // The root (the Working directory) doesn't want any output by itself.
                GenerateSourceFiles = false
            };
            Debug.Assert( unified.CompileOption == CompileOption.None );
            // Assemblies and types are the union of the assemblies and types of the bin paths.
            unified.Assemblies.AddRange( configurations.SelectMany( b => b.Configuration.Assemblies ) );

            var fusion = new Dictionary<string, BinPathConfiguration.TypeConfiguration>();
            foreach( var c in configurations.SelectMany( b => b.Configuration.Types ) )
            {
                if( fusion.TryGetValue( c.Name, out var exists ) )
                {
                    if( !c.Optional ) exists.Optional = false;
                    if( exists.Kind != c.Kind )
                    {
                        monitor.Error( $"Invalid Type configuration across BinPaths for '{c.Name}': {exists.Kind} vs. {c.Kind}." );
                        return null;
                    }
                }
                else fusion.Add( c.Name, new BinPathConfiguration.TypeConfiguration( c.Name, c.Kind, c.Optional ) );
            }
            unified.Types.AddRange( fusion.Values );
            unified.ExcludedTypes.AddRange( globalExcludedTypes );
            return unified;
        }

    }
}
