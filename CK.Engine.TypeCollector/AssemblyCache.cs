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
        readonly Dictionary<GroupKey, BinPathGroup> _binPaths;
        bool _registrationClosed;

        /// <inheritdoc />
        public IReadOnlyCollection<CachedAssembly> Assemblies => _assemblies.Values;

        /// <inheritdoc />
        public CachedAssembly FindOrCreate( Assembly assembly )
        {
            Throw.CheckArgument( assembly is not null && assembly.IsDynamic is false );
            Throw.CheckState( _registrationClosed is true );
            // Note that whatever the kind is (even a Exclude[Engine thats should be a warning), we
            // don't care. The goal after the inital registration is just to collect the assemblies.
            return FindOrCreate( assembly, null, null, out var _ );
        }

        /// <summary>
        /// Encapsulates the work of this <see cref="AssemblyCache"/> on an engine configuration.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to process.</param>
        /// <returns>The result (<see cref="Result.Success"/> can be false).</returns>
        public static Result Run( IActivityMonitor monitor, EngineConfiguration configuration )
        {
            bool success = true;
            var c = new AssemblyCache( configuration.ExcludedAssemblies.Contains );
            foreach( var b in configuration.BinPaths )
            {
                success &= c.Register( monitor, b ).Success;
            }
            // Any new CachedAssemblyInfo will not be IsInitialAssembly.
            c._registrationClosed = true;
            // Dumps a summary.
            var sb = new StringBuilder( success ? "Successfully analyzed " : "Error while analyzing " );
            sb.Append( c._binPaths.Count ).AppendLine( " assembly configurations:" );
            foreach( var b in c._binPaths.Values )
            {
                if( b.Success )
                {
                    sb.AppendLine( $"- {b.ConfiguredTypes.AllTypes.Count} types for group '{b.GroupName}'." );
                    sb.AppendLine( $"  From head PFeatures: '{b.HeadAssemblies.Select( p => p.Name ).Concatenate( "', '" )}'." );
                }
                else
                {
                    sb.AppendLine( $"- Group '{b.GroupName}' failed." );
                }
            }
            monitor.Log( success ? LogLevel.Info : LogLevel.Error, sb.ToString() );
            return new Result( success, c, c._binPaths.Values );
        }

        /// <summary>
        /// Gets the <see cref="AppContext.BaseDirectory"/> from which assemblies are loaded.
        /// </summary>
        NormalizedPath AppContextBaseDirectory => _appContextPath;

        /// <summary>
        /// Initializes a new <see cref="AssemblyCache"/>.
        /// </summary>
        /// <param name="assemblyExcluder">Optional filter that can exclude an assembly when returning true.</param>
        AssemblyCache( Func<string, bool>? assemblyExcluder = null )
        {
            _appContextPath = AppContext.BaseDirectory;
            _assemblyExcluder = assemblyExcluder;
            _assemblies = new Dictionary<object, CachedAssembly>();
            _binPaths = new Dictionary<GroupKey, BinPathGroup>();
        }

        /// <summary>
        /// Registers a <see cref="BinPathAspectConfiguration"/>. Configurations are grouped
        /// into <see cref="BinPathGroup"/> when their assembly related configurations are similar.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="configuration">The configuration to register.</param>
        /// <returns></returns>
        BinPathGroup Register( IActivityMonitor monitor, BinPathConfiguration configuration )
        {
            var k = new GroupKey( configuration );
            if( !_binPaths.TryGetValue( k, out var binPath ) )
            {
                using( monitor.OpenInfo( $"Analyzing configuration '{configuration.Name}' Path '{configuration.Path}'." ) )
                {
                    binPath = new BinPathGroup( this, configuration );
                    bool success = configuration.DiscoverAssembliesFromPath
                                    ? binPath.DiscoverFolder( monitor )
                                    : configuration.Assemblies.Aggregate( true, ( success, b ) => success &= binPath.AddExplicit( monitor, b ) );
                    Throw.DebugAssert( success == binPath.Success );
                    success &= binPath.FinalizeAndCollectTypes( monitor );
                    if( !success )
                    {
                        monitor.CloseGroup( "Failed." );
                    }
                    _binPaths.Add( k, binPath );
                }
            }
            else 
            {
                if( binPath.Success )
                {
                    monitor.Info( $"BinPath configuration '{configuration.Name}' is shared with '{binPath.Configurations.First().Name}'." );
                }
                binPath.AddConfiguration( configuration );
            }
            return binPath;
        }

        CachedAssembly? Find( string name ) => _assemblies.GetValueOrDefault( name );

        CachedAssembly FindOrCreate( Assembly assembly, AssemblyName? knownName, DateTime? knownLastWriteTime, out AssemblyKind? initialKind )
        {
            if( !_assemblies.TryGetValue( assembly, out var cached ) )
            {
                GetAssemblyNames( knownName ?? assembly.GetName(), out string assemblyName, out string assemblyFullName );
                initialKind = AssemblyKind.None;
                if( IsSystemSkipped( assemblyName ) )
                {
                    initialKind = AssemblyKind.SystemSkipped;
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

            static bool IsSystemSkipped( string assemblyName )
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
            return cached;
        }
    }
}
