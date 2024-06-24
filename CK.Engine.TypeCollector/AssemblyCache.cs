using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CK.Engine.TypeCollector
{
    public sealed partial class AssemblyCache : IAssemblyCache
    {
        // Don't be tempted to move this excluder down to each BinPath as
        // this woult break the PFeature sharing accross BinPath (the CachedAssembly
        // would not be able to host the PFeatures, introducing another cache layer in the
        // architecture. Exclusions can be Global or not.
        readonly Func<string, bool>? _assemblyExcluder;
        readonly NormalizedPath _appContextPath;
        // CachedAssembly are indexed by their Assembly and their simple name.
        readonly Dictionary<object, CachedAssembly> _assemblies;
        // BinPaths are indexed by their BinPathKey.
        readonly Dictionary<GroupKey, BinPathGroup?> _binPaths;
        readonly IncrementalHash _hasher;
        List<BinPathGroup>? _result;
        bool _registrationClosed;

        /// <summary>
        /// Initializes a new <see cref="AssemblyCache"/>.
        /// </summary>
        /// <param name="assemblyExcluder">Optional filter that can exclude an assembly when returning true.</param>
        public AssemblyCache( Func<string, bool>? assemblyExcluder = null )
        {
            _appContextPath = AppContext.BaseDirectory;
            _assemblyExcluder = assemblyExcluder;
            _assemblies = new Dictionary<object, CachedAssembly>();
            _binPaths = new Dictionary<GroupKey, BinPathGroup?>();
            _hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
            _result = new List<BinPathGroup>();
        }

        /// <summary>
        /// Gets the <see cref="AppContext.BaseDirectory"/> from which assemblies are loaded.
        /// </summary>
        public NormalizedPath AppContextBaseDirectory => _appContextPath;

        /// <summary>
        /// Registers a <see cref="BinPathAspectConfiguration"/>. Configurations are grouped
        /// into <see cref="BinPathGroup"/> when their assembly related configurations are similar.
        /// <para>
        /// This is idempotent.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to register.</param>
        /// <returns></returns>
        public BinPathGroup? Register( IActivityMonitor monitor, BinPathConfiguration configuration )
        {
            Throw.CheckNotNullArgument( configuration );
            Throw.CheckState( _registrationClosed is false );

            var k = new GroupKey( configuration );
            if( !_binPaths.TryGetValue( k, out var binPath ) )
            {
                using( monitor.OpenInfo( $"Analyzing configuration '{configuration.Name}' Path '{configuration.Path}'." ) )
                {
                    binPath = new BinPathGroup( this, configuration );
                    bool success = configuration.DiscoverAssembliesFromPath
                                    ? binPath.DiscoverFolder( monitor )
                                    : configuration.Assemblies.Aggregate( true, ( success, b ) => success &= binPath.AddExplicit( monitor, b ) );
                    if( success )
                    {
                        success &= binPath.CollectTypes( monitor );
                    }
                    if( !success )
                    {
                        monitor.CloseGroup( "Failed." );
                        _result = null;
                        binPath = null;
                    }
                    _binPaths.Add( k, binPath );
                }
            }
            else if( binPath != null )
            {
                monitor.Info( $"BinPath configuration '{configuration.Name}' is shared with '{binPath.Configurations.First().Name}'." );
                binPath.AddConfiguration( configuration );
            }
            return binPath;
        }

        /// <summary>
        /// Closes this collector, returning the <see cref="BinPathGroup"/> with their similar assembly related <see cref="BinPathGroup.Configurations"/>.
        /// <para>
        /// Depending on the similarity of the types related configurations (<see cref="BinPathConfiguration.ExcludedTypes"/> and <see cref="BinPathConfiguration.Types"/>),
        /// shared type collector may be used for a <see cref="BinPathGroup"/>.
        /// </para>
        /// <para>
        /// This is idempotent.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The <see cref="BinPathGroup"/> results ot null on error.</returns>
        public IReadOnlyCollection<BinPathGroup>? CloseRegistrations( IActivityMonitor monitor )
        {
            if( !_registrationClosed )
            {
                _registrationClosed = true;
                if( _result != null )
                {
                    if( _binPaths.Count == 0 )
                    {
                        monitor.Info( "No assembly registrations, only explicit types will be registered." );
                    }
                    else
                    {
                        _result.AddRange( _binPaths.Values! );
                        var sb = new StringBuilder();
                        sb.Append( "Analyzed " ).Append( _result.Count ).AppendLine( " assembly configurations:" );
                        foreach( var b in _result )
                        {
                            sb.AppendLine( $"- {b.ConfiguredTypes.AllTypes.Count} types for BinPath '{b.GroupName}'." );
                            sb.AppendLine( $"  From primary PFeatures: '{b.HeadAssemblies.Select( p => p.Name ).Concatenate( "', '" )}'." );
                        }
                    }
                }
            }
            return _result;
        }

        /// <inheritdoc />
        public IReadOnlyCollection<CachedAssembly> Assemblies => _assemblies.Values;

        /// <inheritdoc />
        public CachedAssembly FindOrCreate( Assembly assembly )
        {
            Throw.CheckArgument( assembly is not null && assembly.IsDynamic is false );
            Throw.CheckState( _registrationClosed is true );
            return FindOrCreate( assembly, null, null, out var _ );
        }

        CachedAssembly? Find( string name ) => _assemblies.GetValueOrDefault( name );

        CachedAssembly FindOrCreate( Assembly assembly, AssemblyName? knownName, DateTime? knownLastWriteTime, out AssemblyKind? initialKind )
        {
            if( !_assemblies.TryGetValue( assembly, out var cached ) )
            {
                GetAssemblyNames( knownName ?? assembly.GetName(), out string assemblyName, out string assemblyFullName );
                initialKind = AssemblyKind.None;
                if( IsSkipped( assemblyName ) )
                {
                    initialKind = AssemblyKind.Skipped;
                }
                else if( _assemblyExcluder != null && _assemblyExcluder( assemblyName ) )
                {
                    initialKind = AssemblyKind.Excluded;
                }
                cached = DoCreateAndCache( assembly, assemblyName, assemblyFullName, initialKind.Value, knownLastWriteTime );
                return cached;
            }
            initialKind = null;
            return cached;

            static void GetAssemblyNames( AssemblyName aName, out string assemblyName, out string assemblyFullName )
            {
                assemblyName = aName.Name!;
                assemblyFullName = aName.FullName;
                if( string.IsNullOrWhiteSpace( assemblyName ) || string.IsNullOrWhiteSpace( assemblyFullName ) )
                {
                    Throw.ArgumentException( "Invalid assembly: the AssemmblyName.Name or assemblyName.FullName is null, empty or whitespace." );
                }
            }

            static bool IsSkipped( string assemblyName )
            {
                return assemblyName.StartsWith( "Microsoft." )
                       || assemblyName.StartsWith( "System." );
            }

        }

        CachedAssembly DoCreateAndCache( Assembly assembly,
                                         string assemblyName,
                                         string assemblyFullName,
                                         AssemblyKind initialKind,
                                         DateTime? knownLastWriteTime )
        {
            CachedAssembly? cached;
            if( !knownLastWriteTime.HasValue )
            {
                var tryPath = $"{_appContextPath}{assemblyName}.dll";
                knownLastWriteTime = File.Exists( tryPath )
                                        ? File.GetLastWriteTimeUtc( tryPath )
                                        : Util.UtcMinValue;
            }

            var lastWriteTime = knownLastWriteTime.Value;
            cached = new CachedAssembly( assembly, assemblyName, assemblyFullName, initialKind, lastWriteTime, isInitialAssembly: !_registrationClosed );
            _assemblies.Add( assembly, cached );
            if( _assemblies.ContainsKey( assemblyName ) )
            {
                Throw.CKException( $"""
                                        Duplicate assembly name '{assemblyName}': an assembly with this name has already been registered:
                                        - FullName: '{assemblyFullName}', LastWriteTime: {lastWriteTime}.
                                        - FullName: '{cached.FullName}', LastWriteTime: {cached.LastWriteTimeUtc}.
                                        """ );
            }
            _assemblies.Add( assemblyName, cached );
            AddHash( _hasher, cached );
            return cached;
        }

        static void AddHash( IncrementalHash hasher, CachedAssembly assembly )
        {
            hasher.AppendData( MemoryMarshal.Cast<char, byte>( assembly.Name.AsSpan() ) );
            var t = assembly.LastWriteTimeUtc;
            hasher.AppendData( MemoryMarshal.AsBytes( MemoryMarshal.CreateReadOnlySpan( ref t, 1 ) ) );
        }
    }
}
