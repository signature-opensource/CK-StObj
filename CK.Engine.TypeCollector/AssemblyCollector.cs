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

namespace CK.Engine.TypeCollector
{
    public sealed partial class AssemblyCollector
    {
        readonly Func<string, bool>? _configureExclude;
        readonly NormalizedPath _appContextPath;
        // CachedAssembly are indexed by their Assembly and their simple name.
        readonly Dictionary<object, CachedAssembly> _assemblies;
        // BinPath are indexed by their BinPathKey.
        readonly Dictionary<BinPathKey, BinPath?> _binPaths;
        readonly IncrementalHash _hasher;
        bool _closedRegistration;

        /// <summary>
        /// Initializes a new <see cref="AssemblyCollector"/>.
        /// </summary>
        /// <param name="binPath">The path that contains the assemblies.</param>
        /// <param name="configureExclude">Optional filter that can exclude an assembly when returning true.</param>
        public AssemblyCollector( Func<string, bool>? configureExclude = null )
        {
            _appContextPath = AppContext.BaseDirectory;
            _configureExclude = configureExclude;
            _assemblies = new Dictionary<object, CachedAssembly>();
            _binPaths = new Dictionary<BinPathKey, BinPath?>();
            _hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
        }

        public NormalizedPath AppContextBaseDirectory => _appContextPath;

        public BinPath? Ensure( IActivityMonitor monitor, BinPathConfiguration configuration )
        {
            var k = new BinPathKey( configuration );
            if( !_binPaths.TryGetValue( k, out var binPath ) )
            {
                binPath = new BinPath( this, configuration );
                bool success = configuration.DiscoverAssembliesFromPath
                                ? binPath.DiscoverFolder( monitor )
                                : configuration.Assemblies.Aggregate( true, (success,b) => success &= binPath.AddExplicit( monitor, b ) );
                if( !success ) binPath = null;
                _binPaths.Add( k, binPath );
            }
            else if( binPath != null ) 
            {
                binPath.AddConfiguration( configuration );
            }
            return binPath;
        }

        public 

        CachedAssembly? Find( string name ) => _assemblies.GetValueOrDefault( name ); 

        CachedAssembly FindOrCreate( Assembly assembly, AssemblyName aName, DateTime? knownLastWriteTime, out AssemblyKind? initialKind )
        {
            if( !_assemblies.TryGetValue( assembly, out var cached ) )
            {
                GetAssemblyNames( aName, out string assemblyName, out string assemblyFullName );
                initialKind = AssemblyKind.None;
                if( IsSkipped( assemblyName ) )
                {
                    initialKind = AssemblyKind.Skipped;
                }
                else if( _configureExclude != null && _configureExclude( assemblyName ) )
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
            cached = new CachedAssembly( assembly, assemblyName, assemblyFullName, initialKind, lastWriteTime, isInitialAssembly: !_closedRegistration );
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
