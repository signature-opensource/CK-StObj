using CK.Core;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Text;

namespace CK.Setup;

public sealed partial class EngineConfiguration // Normalize
{
    /// <summary>
    /// Idempotent normalization of this configuration:
    /// <list type="number">
    ///     <item>
    ///     Ensures that <see cref="BasePath"/> is rooted, if not use the current directory.
    ///     </item>
    ///     <item>
    ///     Normalize empty <see cref="BaseSHA1"/> to <see cref="SHA1Value.Zero"/>.
    ///     </item>
    ///     <item>
    ///     Handles the [alt|ernative] slots that may appear in the <see cref="BinPathConfiguration.Path"/>:
    ///     <list type="number">
    ///         <item>
    ///         Checks that the <see cref="FirstBinPath"/> defines all the alternative
    ///         and following BinPaths use a subset of them.
    ///         </item>
    ///         <item>
    ///         Selects the alternative based on the most recent changes of any file in the alternative paths.
    ///         </item>
    ///         <item>
    ///         Updates all the <see cref="BinPathConfiguration.Path"/> with the selected alternative.
    ///         </item>
    ///     </list>
    ///     </item>
    ///     <item>
    ///     Removes types from <see cref="Types"/> that appears in <see cref="ExcludedTypes"/>.
    ///     </item>
    ///     <item>
    ///         For each BinPathConfiguration:
    ///         <list type="number">
    ///             <item>
    ///             Empty <see cref="BinPathConfiguration.OutputPath"/> defaults to its <see cref="BinPathConfiguration.Path"/> or is made absolute.
    ///             </item>
    ///             <item>
    ///             Empty <see cref="BinPathConfiguration.ProjectPath"/> defaults to the OutputPath or is made absolute. Ensures that it ends with "/$StObjGen".
    ///             </item>
    ///             <item>
    ///             Applies the '{BasePath}', '{OutputPath}' and '{ProjectPath}' prefix placeholders in every <see cref="BinPathAspectConfiguration"/>
    ///             by processing their Xml projection.
    ///             <para>
    ///             Elements and attribute value that start with '{BasePath}', '{OutputPath}' and '{ProjectPath}' are evaluated in
    ///             every <see cref="BinPathAspectConfiguration.ToXml()"/>
    ///             </para>
    ///             <para>
    ///             If the xml has changed, <see cref="BinPathAspectConfiguration.InitializeFrom(XElement)"/>
    ///             is called to update the BinPath aspect configuration.
    ///             </para>
    ///             </item>
    ///             <item>
    ///             Normalize <see cref="BinPathConfiguration.DiscoverAssembliesFromPath"/>:
    ///             <list type="bullet">
    ///                 <item>
    ///                 When true and Assemblies is non empty, it is set to false.
    ///                 </item>
    ///                 <item>
    ///                 When false and no Assemblies and no Types, it is set to true.
    ///                 </item>
    ///             </list>
    ///             </item>
    ///             <item>
    ///             Removes assemblies from <see cref="BinPathConfiguration.Assemblies"/> that appears in <see cref="ExcludedAssemblies"/>.
    ///             </item>
    ///             <item>
    ///             Propagate the <see cref="ExcludedTypes"/> to <see cref="BinPathConfiguration.ExcludedTypes"/> and
    ///             the <see cref="Types"/> to <see cref="BinPathConfiguration.Types"/>.
    ///             </item>
    ///             <item>
    ///             Removes types from <see cref="BinPathConfiguration.Types"/> that appears in ExcludedTypes.
    ///             </item>
    ///         </list>
    ///     </item>
    /// </list>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="traceNormalizedConfiguration">
    /// False to skip emitting the normalized configuration as a trace (the initial
    /// configuration is always traced in a Info group).
    /// </param>
    /// <returns>True on success, false is something's wrong in the configuration.</returns>
    public bool NormalizeConfiguration( IActivityMonitor monitor, bool traceNormalizedConfiguration = true )
    {
        using( monitor.OpenInfo( $"Normalizing engine configuration:{Environment.NewLine}{ToXml()}" ) )
        {
            if( !Normalize( monitor, this ) )
            {
                monitor.CloseGroup( "Failed." );
                return false;
            }
            if( traceNormalizedConfiguration )
            {
                monitor.Trace( $"Normalized to:{Environment.NewLine}{ToXml()}" );
            }
        }
#if DEBUG
        // Check idempotence and Xml serialization.
        var xml = ToXml();
        var sXml = xml.ToString();
        var reloaded = new EngineConfiguration( xml );
        var xmlReloaded = reloaded.ToXml();
        var sReloaded = xmlReloaded.ToString();
        if( !Normalize( monitor, reloaded ) )
        {
            Throw.CKException( $"Normalization of reloaded {Environment.NewLine}{sReloaded}{Environment.NewLine}Failed. Initial configuration:{Environment.NewLine}{sXml}" );
        }
        if( sXml != sReloaded )
        {
            Throw.CKException( $"Reloaded {Environment.NewLine}{sReloaded}{Environment.NewLine}Difffer from:{Environment.NewLine}{sXml}" );

        }
#endif
        return true;
    }

    /// <summary>
    /// Analyzes all [X|Y...] alternatives inside <see cref="NormalizedPath.Parts"/>.
    /// Note that brackets without | inside are ignored: only patterns with at least one | in brackets are
    /// considered.
    /// </summary>
    readonly struct AlternativePath : IReadOnlyList<string>
    {
        static readonly Regex _regex = new Regex( @"\[(?<1>[^/\]]+)(\|(?<1>[^/\]]+))+\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture );
        readonly IReadOnlyList<AlternativeSlot>? _slots;
        readonly NormalizedPath _originPath;
        readonly NormalizedPath _path;

        /// <summary>
        /// Models a [sl|ot].
        /// </summary>
        public readonly struct AlternativeSlot
        {
            internal AlternativeSlot( int pos, int length, IReadOnlyList<string> alternatives )
            {
                Index = pos;
                Length = length;
                Alternatives = alternatives;
            }

            /// <summary>
            /// Gets the index in the <see cref="NormalizedPath.Path"/> of the start of the
            /// open bracket of the [sl|ot].
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets the length of the [sl|ot].
            /// </summary>
            public int Length { get; }

            /// <summary>
            /// Gets the different alternatives in the slot.
            /// </summary>
            public IReadOnlyList<string> Alternatives { get; }
        }

        /// <summary>
        /// Initializes a new <see cref="AlternativePath"/>.
        /// </summary>
        /// <param name="path">The initial BinPath path.</param>
        public AlternativePath( NormalizedPath path )
        {
            Throw.DebugAssert( path.IsRooted );
            _originPath = path;
            _path = path;
            Match m = _regex.Match( path.Path );
            if( m.Success )
            {
                int count = 1;
                var slots = new List<AlternativeSlot>();
                do
                {
                    // Sort the options.
                    var a = m.Groups[1].Captures.Cast<Capture>().Select( c => c.Value ).ToArray();
                    Array.Sort( a );
                    slots.Add( new AlternativeSlot( m.Index, m.Length, a ) );
                    count *= a.Length;
                }
                while( (m = m.NextMatch()).Success );
                _slots = slots;
                Count = count;
            }
            else
            {
                _slots = null;
                Count = 1;
            }
        }

        /// <summary>
        /// Gets the initial path value.
        /// </summary>
        public NormalizedPath OrginPath => _originPath;

        /// <summary>
        /// Gets the path for which alternatives have been analysed.
        /// </summary>
        public NormalizedPath Path => _path;

        /// <summary>
        /// Gets whether this struct is valid or is the default one.
        /// </summary>
        public bool IsNotDefault => _slots != null;

        /// <summary>
        /// Gets the variable slots possibilites.
        /// </summary>
        public IReadOnlyList<AlternativeSlot> AlternativeSlots => _slots ?? Array.Empty<AlternativeSlot>();

        /// <summary>
        /// Gets the total number of combinations.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets one of the possible path.
        /// </summary>
        /// <param name="i">The possible path from 0 to <see cref="Count"/> (excluded).</param>
        /// <returns>The path.</returns>
        public string this[int i]
        {
            get
            {
                Throw.CheckOutOfRangeArgument( i >= 0 && i < Count );
                StringBuilder b = new StringBuilder();
                int idxP = 0;
                for( int iSlot = 0; iSlot < AlternativeSlots.Count; ++iSlot )
                {
                    var a = AlternativeSlots[iSlot];
                    var c = a.Alternatives.Count;
                    b.Append( _path.Path, idxP, a.Index - idxP )
                             .Append( a.Alternatives[i % c] );
                    idxP = a.Index + a.Length;
                    i /= c;
                }
                b.Append( _path.Path, idxP, _path.Path.Length - idxP );
                return b.ToString();
            }
        }

        /// <summary>
        /// Gets one of the possible choice among the different <see cref="AlternativeSlots"/>.
        /// </summary>
        /// <param name="i">The possible choice from 0 to <see cref="Count"/> (excluded).</param>
        /// <returns>The path.</returns>
        public string[] Choose( int i )
        {
            Throw.CheckOutOfRangeArgument( i >= 0 && i < Count );
            var r = new string[AlternativeSlots.Count];
            for( int iSlot = 0; iSlot < AlternativeSlots.Count; ++iSlot )
            {
                var a = AlternativeSlots[iSlot];
                var c = a.Alternatives.Count;
                r[iSlot] = a.Alternatives[i % c];
                i /= c;
            }
            return r;
        }

        /// <summary>
        /// Checks whether this alternative can be applied to another one:
        /// the other one must contain a subset of our <see cref="AlternativeSlots"/>.
        /// </summary>
        /// <param name="other">The other alternative path.</param>
        /// <returns>True if this one can cover the other.</returns>
        public bool CanCover( in AlternativePath other )
        {
            foreach( var a in other.AlternativeSlots )
            {
                if( FindSlotIndex( a ) < 0 ) return false;
            }
            return true;
        }

        /// <summary>
        /// Apply the choice from this path to another alternate path.
        /// The other one must contain a subset of our <see cref="AlternativeSlots"/>.
        /// </summary>
        /// <param name="i">The possible choice from 0 to <see cref="Count"/> (excluded).</param>
        /// <param name="other">The other alternative path.</param>
        /// <returns>The resulting path.</returns>
        public string Cover( int i, in AlternativePath other )
        {
            var c = Choose( i );
            StringBuilder b = new StringBuilder();
            int idxP = 0;
            for( int iSlot = 0; iSlot < other.AlternativeSlots.Count; ++iSlot )
            {
                var a = other.AlternativeSlots[iSlot];
                int idx = FindSlotIndex( a );
                Throw.CheckState( nameof( CanCover ), idx >= 0 );
                b.Append( other._path.Path, idxP, a.Index - idxP )
                 .Append( c[idx] );
                idxP = a.Index + a.Length;
            }
            b.Append( other._path.Path, idxP, other._path.Path.Length - idxP );
            return b.ToString();
        }

        int FindSlotIndex( AlternativeSlot other ) => AlternativeSlots.IndexOf( a => a.Alternatives.SequenceEqual( other.Alternatives ) );

        /// <summary>
        /// Returns the possible alternatives.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<string> GetEnumerator()
        {
            var capture = this;
            return Enumerable.Range( 0, Count ).Select( i => capture[i] ).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    static bool Normalize( IActivityMonitor monitor, EngineConfiguration c )
    {
        Throw.DebugAssert( c.BinPaths.Select( b => b.Name ).Distinct().Count() == c.BinPaths.Count );

        c.BasePath = CheckEnginePaths( monitor, c );
        if( c.BasePath.IsEmptyPath ) return false;

        // Process the BinPaths: setup the AlternativePath for each of them.
        AlternativePath[] altPaths = AnalyzeBinPaths( c, out var maxAlternativeCount );

        bool success = ResolveAlternateBinPaths( monitor, c, maxAlternativeCount, altPaths );
        CheckTypeConfigurationSet( monitor, c.Types, c.ExcludedTypes, null, ref success );
        FinalizeBinPaths( monitor, c, ref success );
        return success;

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

        static void FinalizeBinPaths( IActivityMonitor monitor, EngineConfiguration c, ref bool success )
        {
            foreach( BinPathConfiguration b in c.BinPaths )
            {
                Finalize( monitor, c, b, ref success );
            }

            static void Finalize( IActivityMonitor monitor, EngineConfiguration c, BinPathConfiguration b, ref bool success )
            {
                if( b.OutputPath.IsEmptyPath ) b.OutputPath = b.Path;
                else b.OutputPath = MakeAbsolutePath( c, b.OutputPath );

                if( b.ProjectPath.IsEmptyPath ) b.ProjectPath = b.OutputPath;
                else
                {
                    b.ProjectPath = MakeAbsolutePath( c, b.ProjectPath );
                    if( b.ProjectPath.LastPart.Equals( "$StObjGen", StringComparison.OrdinalIgnoreCase ) )
                    {
                        b.ProjectPath = b.ProjectPath.RemoveLastPart();
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

                // Cleanup Assemblies.
                foreach( var a in c.ExcludedAssemblies )
                {
                    if( b.Assemblies.Remove( a ) )
                    {
                        monitor.Warn( $"BinPath '{b.Name}' Assembly contains '{a}' that is excluded by EngineConfiguration. It is removed and will be ignored." );
                    }
                }
                // Handles Types
                b.ExcludedTypes.AddRange( c.ExcludedTypes );
                // Check the Types and removes the excluded ones before adding the Engine level Types (that have already been checked)
                // if they are on errors, we don't care: the error has been already emitted.
                CheckTypeConfigurationSet( monitor, b.Types, b.ExcludedTypes, b, ref success );
                // Adds the Engine level types.
                b.Types.UnionWith( c.Types );
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

        static void CheckTypeConfigurationSet( IActivityMonitor monitor, HashSet<Type> types, HashSet<Type> excludedTypes, BinPathConfiguration? b, ref bool success )
        {
            List<Type>? excluded = null;
            string? source = null;
            foreach( var tc in types )
            {
                if( excludedTypes.Contains( tc ) )
                {
                    source ??= b != null ? $"BinPath '{b.Name}' Types" : "Global Types";
                    monitor.Warn( $"{source} contains '{tc:N}' that is excluded. It is removed and will be ignored." );
                    excluded ??= new List<Type>();
                    excluded.Add( tc );
                }
            }
            if( excluded != null )
            {
                foreach( var tc in excluded )
                {
                    types.Remove( tc );
                }
            }
        }

    }
}
