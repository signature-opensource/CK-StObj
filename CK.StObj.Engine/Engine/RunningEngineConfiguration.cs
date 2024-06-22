using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace CK.Setup
{
    /// <summary>
    /// Implements <see cref="IRunningEngineConfiguration"/>.
    /// </summary>
    public sealed partial class RunningEngineConfiguration : IRunningEngineConfiguration
    {
        readonly List<RunningBinPathGroup> _binPathGroups;

        public RunningEngineConfiguration( EngineConfiguration configuration )
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
        /// Resolves the [alt|ernative] slots that may appear in the <see cref="BinPathConfiguration.Path"/>:
        /// each path is updated and rooted.
        /// <para>
        /// Any element or attribute value that start with '{BasePath}', '{OutputPath}' and '{ProjectPath}' are evaluated
        /// in every <see cref="BinPathAspectConfiguration.ToXml()"/> and if the xml has changed, <see cref="BinPathAspectConfiguration.InitializeFrom(XElement)"/>
        /// is called to update the bin path aspect configuration.
        /// </para>
        /// <para>
        /// This method is public to ease tests.
        /// </para>
        /// </summary>
        /// <returns>True on success, false is something's wrong.</returns>
        public static bool PrepareConfiguration( IActivityMonitor monitor, EngineConfiguration c )
        {
            c.BasePath = CheckEnginePaths( monitor, c );
            if( c.BasePath.IsEmptyPath ) return false;

            // Process the BinPaths:
            // - Setting default Name
            // - Setup the AlternativePath for each of them.
            AlternativePath[] altPaths = AnalyzeBinPaths( c, out var maxAlternativeCount );

            // Check that BinPath name is unique. This must be done after the loop above (Name is set when empty).
            var byName = c.BinPaths.GroupBy( c => c.Name );
            if( byName.Any( g => g.Count() > 1 ) )
            {
                monitor.Error( $"BinPath configuration 'Name' must be unique. Duplicates found: {byName.Where( g => g.Count() > 1 ).Select( g => g.Key ).Concatenate()}" );
                return false;
            }

            // Handle [Alter|native] BinPath.Path if there are: all BinPath.Paths are now settled. 
            if( !ResolveAlternateBinPaths( monitor, c, maxAlternativeCount, altPaths ) )
            {
                return false;
            }

            // For each BinPathConfiguration:
            // - Any OutputPath empty is set to its Path or is made absolute.
            // - Any ProjectPath empty is set to the OutputPath or is made absolute. Ensures that it ends with "/$StObjGen".
            // - Process BinPath Xml to handle '{BasePath}', '{OutputPath}' and '{ProjectPath}' prefixes.
            // - DiscoverAssembliesFromPath:
            //   - When true and non empty Assemblies, it is set to false.
            //   - When false and no Assemblies and no Types, it is set to true.
            // - Propagate GlobalExcludedTypes to each BinPath ExcludedTypes.
            // - Removes BinPath Types that appears in their ExcludedTypes (warn on them).
            FinalizeBinPaths( monitor, c );

            return true;

            /// <summary>
            /// Ensures that <see cref="BinPathConfiguration.Path"/>, <see cref="BinPathConfiguration.OutputPath"/>
            /// are rooted.
            /// </summary>
            /// <returns>Non empty path on success.</returns>
            static NormalizedPath CheckEnginePaths( IActivityMonitor monitor, EngineConfiguration c )
            {
                // Roots the BasePath.
                NormalizedPath basePath = c.BasePath;
                if( basePath.IsEmptyPath )
                {
                    basePath = Environment.CurrentDirectory;
                    monitor.Info( $"Configuration BasePath is empty: using current directory '{basePath}'." );
                }
                else if( !basePath.IsRooted )
                {
                    basePath = Path.GetFullPath( basePath );
                    monitor.Info( $"Configuration BasePath changed from '{c.BasePath}' to '{basePath}'." );
                }
                // Checks the GeneratedAssemblyName (no error, just a fix) and normalize BaseSHA1 to Zero.
                if( c.GeneratedAssemblyName.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) )
                {
                    monitor.Info( $"GeneratedAssemblyName should not end with '.dll'. Removing suffix." );
                    c.GeneratedAssemblyName += c.GeneratedAssemblyName.Substring( 0, c.GeneratedAssemblyName.Length - 4 );
                }
                if( c.BaseSHA1.IsZero || c.BaseSHA1 == SHA1Value.Empty )
                {
                    c.BaseSHA1 = SHA1Value.Zero;
                    monitor.Info( $"Zero or Empty BaseSHA1, the generated code source SHA1 will be used." );
                }
                return basePath;
            }

            static AlternativePath[] AnalyzeBinPaths( EngineConfiguration c, out int maxAlternativeCount )
            {
                maxAlternativeCount = 1;
                var altPaths = new AlternativePath[c.BinPaths.Count];
                int idx = 0;
                foreach( var b in c.BinPaths )
                {
                    b.Path = MakeAbsolutePath( c, b.Path );

                    var ap = new AlternativePath( b.Path.Path );
                    if( ap.Count > maxAlternativeCount ) maxAlternativeCount = ap.Count;
                    altPaths[idx++] = ap;

                    // Use the incremented idx: first name is "BinPath1".
                    if( String.IsNullOrWhiteSpace( b.Name ) ) b.Name = $"BinPath{idx}";
                }
                return altPaths;
            }

            static NormalizedPath MakeAbsolutePath( EngineConfiguration c, NormalizedPath p )
            {
                if( !p.IsRooted ) p = c.BasePath.Combine( p );
                p = p.ResolveDots();
                return p;
            }

            // Resolves [sl|ots] in every BinPathConfiguration.Path (the EngineConfiguration.FirstBinPath is driving).
            static bool ResolveAlternateBinPaths( IActivityMonitor monitor, EngineConfiguration c, int maxAlternativeCount, AlternativePath[] altPaths )
            {
                if( maxAlternativeCount > 1 )
                {
                    using( monitor.OpenInfo( $"Handling {maxAlternativeCount} possibilities for {c.BinPaths.Count} paths." ) )
                    {
                        var primary = altPaths[0];
                        var alien = altPaths.Skip( 1 ).FirstOrDefault( p => !primary.CanCover( in p ) );
                        if( alien.IsNotDefault )
                        {
                            monitor.Error( $"""
                                            The path '{alien.OrginPath}' must not define alternatives that are NOT defined in the first path '{primary.Path}'.
                                            The first path drives the alternative analysis.
                                            """ );
                            return false;
                        }
                        using( monitor.OpenTrace( $"Testing {primary.Count} alternate paths in {primary.Path}." ) )
                        {
                            int bestIdx = -1;
                            NormalizedPath best = new NormalizedPath();
                            DateTime bestDate = Util.UtcMinValue;
                            for( int i = 0; i < primary.Count; ++i )
                            {
                                NormalizedPath path = primary[i];
                                var noPub = path.LastPart == "publish" ? path.RemoveLastPart() : path;
                                if( !Directory.Exists( noPub ) )
                                {
                                    monitor.Debug( $"Alternate path '{noPub}' not found." );
                                    continue;
                                }
                                var files = Directory.EnumerateFiles( noPub );
                                if( files.Any() )
                                {
                                    var mostRecent = Directory.EnumerateFiles( noPub ).Max( p => File.GetLastWriteTimeUtc( p ) );
                                    if( bestDate < mostRecent )
                                    {
                                        bestDate = mostRecent;
                                        best = path;
                                        bestIdx = i;
                                    }
                                }
                                else monitor.Debug( $"Alternate path '{noPub}' is empty." );
                            }
                            if( bestIdx < 0 )
                            {
                                monitor.Error( $"Unable to find any file in any of the {primary.Count} paths in {primary.Path}." );
                                return false;
                            }
                            monitor.Info( $"Selected path is n°{bestIdx}: {best} since it has the most recent file change ({bestDate})." );
                            c.FirstBinPath.Path = best;
                            for( var iFinal = 1; iFinal < altPaths.Length; ++iFinal )
                            {
                                var aP = altPaths[iFinal];
                                NormalizedPath cap = primary.Cover( bestIdx, aP );
                                if( aP.OrginPath != cap.Path )
                                {
                                    monitor.Trace( $"Path '{altPaths[iFinal].OrginPath}' resolved to '{cap}'." );
                                    c.BinPaths[iFinal].Path = cap;
                                }
                            }
                        }
                    }
                }
                else monitor.Trace( $"No alternative found among the {c.BinPaths.Count} paths." );
                return true;
            }

            static void FinalizeBinPaths( IActivityMonitor monitor, EngineConfiguration c )
            {
                foreach( var b in c.BinPaths )
                {
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

                    bool hasChanged;
                    foreach( var binPathAspect in b.Aspects )
                    {
                        hasChanged = false;
                        var e = binPathAspect.ToXml();
                        Throw.DebugAssert( b.Name != null );
                        EvalKnownPaths( monitor, b.Name, binPathAspect.AspectName, e, c.BasePath, b.OutputPath, b.ProjectPath, ref hasChanged );
                        if( hasChanged ) binPathAspect.InitializeFrom( e );
                    }

                    // Handles DiscoverAssembliesFromPath.
                    if( b.DiscoverAssembliesFromPath )
                    {
                        if( b.Assemblies.Count > 0 )
                        {
                            monitor.Warn( $"BinPath '{b.Name}' has DiscoverAssembliesFromPath but contains {b.Assemblies.Count} Assemblies. Setting DiscoverAssembliesFromPath to false." );
                            b.DiscoverAssembliesFromPath = false;
                        }
                    }
                    else if( b.Assemblies.Count == 0 && b.Types.Count == 0 )
                    {
                        monitor.Info( $"BinPath '{b.Name}' contains no Assemblies and no Types: setting DiscoverAssembliesFromPath to true." );
                        b.DiscoverAssembliesFromPath = true;
                    }

                    // Handles types
                    b.ExcludedTypes.AddRange( c.GlobalExcludedTypes );

                    foreach( var tc in b.Types )
                    {
                        if( b.ExcludedTypes.Remove( tc.Type ) )
                        {
                            monitor.Warn( $"BinPath '{b.Name}' Types contains '{tc.Type}' ({tc.Kind}) that is excluded. It is removed and will be ignored." );
                        }
                    }

                }
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
        }

        /// <summary>
        /// This throws if the CKSetup configuration cannot be used for any reason (missing
        /// expected information).
        /// </summary>
        internal void ApplyCKSetupConfiguration( IActivityMonitor monitor, XElement ckSetupConfig )
        {
            static NormalizedPath MakeAbsolutePath( EngineConfiguration c, NormalizedPath p )
            {
                if( !p.IsRooted ) p = c.BasePath.Combine( p );
                p = p.ResolveDots();
                return p;
            }
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

        internal bool CreateRunningBinPathGroups( IActivityMonitor monitor, out bool canSkipRun )
        {
            // Lets be optimistic (and if an error occurred the returned false will skip the run anyway).
            // If ForceRun is true, we'll always run. This flag can only transition from true to false.
            canSkipRun = !Configuration.ForceRun;
            if( Configuration.BinPaths.Count == 1 )
            {
                _binPathGroups.Add( new RunningBinPathGroup( Configuration, Configuration.FirstBinPath, Configuration.BaseSHA1 ) );
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
                return x.Assemblies.SetEquals( y.Assemblies )
                       && x.ExcludedTypes.SetEquals( y.ExcludedTypes )
                       && x.Types.SetEquals( y.Types );
            }

            // Useless but fulfills the equality contract.
            public int GetHashCode( BinPathConfiguration b ) => b.ExcludedTypes.Count
                                                                + b.Types.Count * 79
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
                                                                        HashSet<Type> globalExcludedTypes )
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
            // Assemblies are the union of the assemblies of the bin paths.
            unified.Assemblies.AddRange( configurations.SelectMany( b => b.Configuration.Assemblies ) );
            // Excluded types are only the global ones.
            unified.ExcludedTypes.AddRange( globalExcludedTypes );

            // Unified is only interested in IPoco and IRealObject (kind is useless).
            // The BinPath Types that also appear in their BinPath ExcludedTypes have already been filtered out,
            // we don't need to remove them.
            Throw.DebugAssert( configurations.All( c => c.Configuration.Types.Select( tc => tc.Type )
                                                                             .Any( t => c.Configuration.ExcludedTypes.Contains( t ) ) is false ) );

            var all = configurations.SelectMany( b => b.Configuration.Types.Select( tc => tc.Type ) )
                                    .Where( t => typeof( IPoco ).IsAssignableFrom( t ) || typeof( IRealObject ).IsAssignableFrom( t ) )
                                    .Select( t => new BinPathConfiguration.TypeConfiguration( t ) );
            unified.Types.AddRange( all );
            return unified;
        }

    }
}
