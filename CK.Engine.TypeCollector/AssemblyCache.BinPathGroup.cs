using CK.Core;
using CK.Setup;
using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace CK.Engine.TypeCollector;


public sealed partial class AssemblyCache // BinPathGroup
{
    /// <summary>
    /// Collects types from one or more similar <see cref="BinPathConfiguration"/>.
    /// </summary>
    public sealed class BinPathGroup
    {
        readonly AssemblyCache _assemblyCache;
        readonly List<BinPathConfiguration> _configurations;
        readonly NormalizedPath _path;
        // The head assemblies are mapped to true when explicitly added.
        readonly SortedDictionary<CachedAssembly, bool> _heads;
        // SystemSkipped stored to avoid too much logs.
        readonly List<CachedAssembly> _systemSkipped;
        readonly bool _isAppContextFolder;
        SHA1Value _signature;

        DateTime _maxFileTime;
        string _groupName;
        // Initially set to ImmutableConfiguredTypeSet.Empty, replaced on success.
        IConfiguredTypeSet _result;
        // Null when unknwon, true when ExplicitAdd is used, false for AddBinPath.
        bool? _explicitMode;
        // Success is set to false at the first error.
        bool _success;

        internal BinPathGroup( AssemblyCache assemblyCache, BinPathConfiguration configuration )
        {
            _assemblyCache = assemblyCache;
            _configurations = new List<BinPathConfiguration> { configuration };
            _groupName = configuration.Name;
            _path = configuration.Path;
            _heads = new SortedDictionary<CachedAssembly, bool>();
            _maxFileTime = Util.UtcMinValue;
            _isAppContextFolder = configuration.Path == _assemblyCache.AppContextBaseDirectory;
            _result = ImmutableConfiguredTypeSet.Empty;
            _systemSkipped = new List<CachedAssembly>();
            _success = true;
        }

        /// <summary>
        /// Gets whether no error occurred so far.
        /// </summary>
        public bool Success => _success;

        /// <summary>
        /// Gets the set of configurations that shares identical assembly related configurations
        /// </summary>
        public IReadOnlyCollection<BinPathConfiguration> Configurations => _configurations;

        internal void AddConfiguration( BinPathConfiguration configuration )
        {
            Throw.DebugAssert( _configurations.Contains( configuration ) is false );
            _configurations.Add( configuration );
            // Don't update the groupName here as we want it to be order by name.
        }

        /// <summary>
        /// Gets this BinPath path shared by all <see cref="Configurations"/>.
        /// </summary>
        public NormalizedPath Path => _path;

        /// <summary>
        /// Gets the comma separated configuration names.
        /// </summary>
        public string GroupName => _groupName;

        /// <summary>
        /// Gets whether this BinPath is the <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public bool IsAppContextFolder => _isAppContextFolder;

        /// <summary>
        /// Get the current set of "head" assemblies.
        /// </summary>
        /// <returns>The set of "head" assemblies.</returns>
        public IReadOnlyCollection<CachedAssembly> HeadAssemblies => _heads.Keys;

        /// <summary>
        /// Gets the assembly cache.
        /// </summary>
        public AssemblyCache AssemblyCache => _assemblyCache;

        /// <summary>
        /// Gets the final types to register from this BinPath.
        /// <para>
        /// When <see cref="Success"/> is false, this is empty.
        /// </para>
        /// </summary>
        public IConfiguredTypeSet ConfiguredTypes => _result;

        /// <summary>
        /// Gets the greatest last write time of the files involved in this group.
        /// </summary>
        public DateTime MaxFileTime => _maxFileTime;

        /// <summary>
        /// Gets the digital signature of this BinPathGroup. Hash is based on:
        /// <list type="bullet">
        ///     <item>The <see cref="Path"/>. If the locattion changed we don't want to take any risk.</item>
        ///     <item>The <see cref="MaxFileTime"/>.</item>
        ///     <item>The <see cref="HeadAssemblies"/>'s Name and LastWriteTime (order by Name).</item>
        /// </list>
        /// This should be enough to detect any change thanks to the fact that referencers are recompiled
        /// whenever one of their reference changes.
        /// </summary>
        public SHA1Value Signature => _signature;

        internal bool DiscoverFolder( IActivityMonitor monitor )
        {
            Throw.DebugAssert( "AddBinPath can be called only once.", _explicitMode is null );
            _explicitMode = false;
            foreach( var f in Directory.EnumerateFiles( _path, "*.dll" ) )
            {
                LoadAssembly( monitor, f, out var _ );
            }
            return _success;
        }

        internal bool AddExplicit( IActivityMonitor monitor, string assemblyName )
        {
            Throw.DebugAssert( !string.IsNullOrWhiteSpace( assemblyName ) );
            Throw.DebugAssert( !assemblyName.EndsWith( "*.dll", StringComparison.OrdinalIgnoreCase ) );
            Throw.DebugAssert( "AddBinPath has been called.", _explicitMode is null or true );
            _explicitMode = true;

            var existingFilePath = _path.AppendPart( assemblyName + ".dll" );
            if( !File.Exists( existingFilePath ) )
            {
                monitor.Error( $"Failed to register assembly: file '{assemblyName}.dll' not found in the bin path '{_path}'." );
                return _success = false;
            }
            if( LoadAssembly( monitor, existingFilePath, out var cachedAssembly ) )
            {
                Throw.DebugAssert( cachedAssembly != null );
                // Explictly adding an engine is a configuration error.
                if( cachedAssembly.Kind == AssemblyKind.Engine )
                {
                    monitor.Error( $"Assembly '{cachedAssembly.Name}' is a CKEngine. Engine assemblies cannot be added to an AssemblyCollector." );
                    return _success = false;
                }
                // In explicit mode, all added assemblies are de facto head assemblies.
                if( cachedAssembly.Kind == AssemblyKind.PFeature )
                {
                    _heads[cachedAssembly] = true;
                }
                else
                {
                    monitor.Warn( $"Assembly '{cachedAssembly.Name}' is not a PFeature but '{cachedAssembly.Kind}'. Ignored." );
                }
            }
            return _success;
        }

        internal bool FinalizeAndCollectTypes( IActivityMonitor monitor )
        {
            if( _configurations.Count > 1 )
            {
                _configurations.Sort( ( a, b ) => a.Name.CompareTo( b.Name ) );
                _groupName = _configurations.Select( b => b.Name ).Concatenate( "-" );
            }
            monitor.Trace( $"Skipped {_systemSkipped.Count} system assemblies: {_systemSkipped.Select( a => a.Name ).Concatenate()}." );
            // Useless to keep the list content.
            _systemSkipped.Clear();
            if( !_success ) return false;

            using var _ = monitor.OpenInfo( $"Collecting types from head PFeatures: '{_heads.Keys.Select( a => a.Name ).Concatenate( "', '" )}'." );
            using var hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
            hasher.Append( _path.Path );

            var c = new ConfiguredTypeSet();
            bool success = true;
            foreach( var head in _heads.Keys )
            {
                head.AddHash( hasher );
                success &= CollectTypes( monitor, head, out var headC );
                c.Add( monitor, headC, head.ToString() );
            }
            _signature = new SHA1Value( hasher, resetHasher: false );
            if( success ) _result = c;
            return success;

            static bool CollectTypes( IActivityMonitor monitor, CachedAssembly assembly, out ConfiguredTypeSet c )
            {
                if( assembly._types != null )
                {
                    c = assembly._types;
                    return true;
                }
                c = new ConfiguredTypeSet();
                var assemblySourceName = assembly.ToString();
                using var _ = monitor.OpenInfo( assemblySourceName );
                bool success = true;
                foreach( var sub in assembly.PFeatures )
                {
                    success &= CollectTypes( monitor, sub, out var subC );
                    c.Add( monitor, subC, assemblySourceName );
                }
                c.AddRange( assembly.AllVisibleTypes );
                // Don't merge the 2 loops here!
                // We must first handle the Add and then the Remove.
                // 1 - Add types.
                List<Type>? changed = null;
                foreach( var a in assembly.CustomAttributes )
                {
                    if( a.AttributeType == typeof( RegisterCKTypeAttribute ) )
                    {
                        var ctorArgs = a.ConstructorArguments;
                        // Constructor (Type, Type[] others):
                        if( ctorArgs[1].Value is Type?[] others )
                        {
                            // Filters out null thanks to "is".
                            if( ctorArgs[0].Value is Type t )
                            {
                                success &= HandleTypeConfiguration( monitor, c, ref changed, add: true, assemblySourceName, t, ConfigurableAutoServiceKind.None );
                            }
                            // Maximal precautions: filters out any potential null.
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleTypeConfiguration( monitor, c, ref changed, add: true, assemblySourceName, o, ConfigurableAutoServiceKind.None );
                            }
                        }
                        else if( ctorArgs[1].Value is ConfigurableAutoServiceKind kind )
                        {
                            // Filters out null thanks to "is".
                            if( ctorArgs[0].Value is Type t )
                            {
                                success &= HandleTypeConfiguration( monitor, c, ref changed, add: true, assemblySourceName, t, kind );
                            }
                        }
                    }
                }
                if( success && changed != null )
                {
                    monitor.Info( $"Assembly '{assembly.Name}' explicitly registers {changed.Count} types: '{changed.Select( t => t.ToCSharpName() ).Concatenate( "', '" )}'." );
                    changed.Clear();
                }
                // 2 - Remove types.
                foreach( var a in assembly.CustomAttributes )
                {
                    if( a.AttributeType == typeof( CK.Setup.ExcludeCKTypeAttribute ) )
                    {
                        var ctorArgs = a.ConstructorArguments;
                        if( ctorArgs[0].Value is Type t )
                        {
                            success &= HandleTypeConfiguration( monitor, c, ref changed, add: false, assemblySourceName, t, ConfigurableAutoServiceKind.None );
                        }
                        if( ctorArgs[1].Value is Type?[] others && others.Length > 0 )
                        {
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleTypeConfiguration( monitor, c, ref changed, add: false, assemblySourceName, o, ConfigurableAutoServiceKind.None );
                            }
                        }
                    }
                }
                if( success && changed != null )
                {
                    monitor.Info( $"Assembly '{assembly.Name}' explicitly removed {changed.Count} types from registration: '{changed.Select( t => t.ToCSharpName() ).Concatenate( "', '" )}'." );
                }
                monitor.CloseGroup( $"{c.AllTypes.Count} types." );
                assembly._types = c;
                return success;

                static bool HandleTypeConfiguration( IActivityMonitor monitor,
                                                     ConfiguredTypeSet c,
                                                     ref List<Type>? changed,
                                                     bool add,
                                                     string sourceAssemblyName,
                                                     Type t,
                                                     ConfigurableAutoServiceKind kind )
                {
                    var error = TypeConfiguration.GetConfiguredTypeErrorMessage( t, kind );
                    if( error != null )
                    {
                        monitor.Error( $"Invalid [assembly:{(add ? "Register" : "Exclude")}CKTypeAttribute] in {sourceAssemblyName}: type '{t:N}' {error}." );
                        return false;
                    }
                    if( add ? c.Add( monitor, sourceAssemblyName, t, kind ) : c.Remove( t ) )
                    {
                        changed ??= new List<Type>();
                        changed.Add( t );
                    }
                    return true;
                }
            }
        }


        // For DiscoverFolder an assembly load error is just a warning. In this mode this may return true with a
        // null out result (and we don't care of the result in this mode).
        // In Explicit mode, a true return implies a non null out result.
        // > Called by AddExplicit and AddBinPath.
        bool LoadAssembly( IActivityMonitor monitor, NormalizedPath existingFilePath, out CachedAssembly? result )
        {
            Throw.DebugAssert( _explicitMode.HasValue );
            if( HandleFile( monitor, existingFilePath, out NormalizedPath appContextFullPath, out DateTime existingFileTime ) )
            {
                try
                {
                    var a = AssemblyLoadContext.Default.LoadFromAssemblyPath( appContextFullPath );
                    result = DoAdd( monitor, a, null, existingFileTime );
                    Throw.DebugAssert( result != null );
                    return true;
                }
                catch( Exception ex )
                {
                    if( _explicitMode.Value )
                    {
                        monitor.Error( $"Error while loading '{existingFilePath.LastPart}'.", ex );
                        result = null;
                        return _success = false;
                    }
                    monitor.Warn( $"Failed to load '{existingFilePath.LastPart}' from '{_assemblyCache.AppContextBaseDirectory}'. Ignored.", ex );
                    result = null;
                    return true;
                }
            }
            Throw.DebugAssert( "Explicit Mode => Success has been set to false.", !_explicitMode.Value || !_success );
            result = null;
            return false;
        }

        // Called by LoadAssembly once the assembly has been loaded.
        // On success, LoadAssembly calls DoAdd.
        bool HandleFile( IActivityMonitor monitor, NormalizedPath existingFile, out NormalizedPath appContextFullPath, out DateTime existingFileTime )
        {
            Throw.DebugAssert( _explicitMode.HasValue );
            if( existingFile.LastPart.StartsWith( "CK.GeneratedAssembly.", StringComparison.OrdinalIgnoreCase ) )
            {
                appContextFullPath = default;
                existingFileTime = default;
                if( _explicitMode.Value )
                {
                    monitor.Error( $"Generated assembly '{existingFile.LastPart}' cannot be an assembly to process." );
                    return _success = false;
                }
                monitor.Warn( $"Ignoring '{existingFile.LastPart}'." );
                return false;
            }

            existingFileTime = File.GetLastWriteTimeUtc( existingFile );
            if( _isAppContextFolder )
            {
                appContextFullPath = existingFile;
            }
            else
            {
                appContextFullPath = _assemblyCache.AppContextBaseDirectory.AppendPart( existingFile.LastPart );
                if( !File.Exists( appContextFullPath ) )
                {
                    if( _explicitMode.Value )
                    {
                        monitor.Error( $"Assembly cannot be registered: file '{existingFile.LastPart}' is not in AppContext.BaseDirectory ({_assemblyCache.AppContextBaseDirectory})." );
                        return _success = false;
                    }
                    monitor.Warn( $"Skipped '{existingFile.LastPart}' since it is not in AppContext.BaseDirectory ({_assemblyCache.AppContextBaseDirectory})." );
                    return false;
                }
                var appContextFileTime = File.GetLastWriteTimeUtc( appContextFullPath );
                if( existingFileTime > appContextFileTime )
                {
                    monitor.Error( $"File '{existingFile.LastPart}' is more recent in '{_path}' than in the AppContext.BaseDirectory '{_assemblyCache.AppContextBaseDirectory}'." );
                    return _success = false;
                }
            }
            if( _maxFileTime < existingFileTime ) _maxFileTime = existingFileTime;
            return true;
        }

        // Called by LoadAssembly.
        // Calls HandleAssemblyReferences that recursively calls this.
        CachedAssembly DoAdd( IActivityMonitor monitor,
                              Assembly assembly,
                              AssemblyName? knownName,
                              DateTime? knownLastWriteTime )
        {
            Throw.DebugAssert( _explicitMode != null );

            var cached = _assemblyCache.FindOrCreate( assembly, knownName, knownLastWriteTime, out AssemblyKind? initialKind );
            // Non null initialKind => It is a new one.
            if( initialKind.HasValue )
            {
                if( cached._kind == AssemblyKind.SystemSkipped )
                {
                    _systemSkipped.Add( cached );
                }
                else
                {
                    if( cached._kind.IsExcludedEngine() )
                    {
                        cached._kind &= ~AssemblyKind.Excluded;
                        monitor.Warn( $"Engine assembly '{cached.Name}' is excluded. Ignoring the exclusion." );
                    }
                    if( cached._kind.IsExcludedPFeatureDefiner() )
                    {
                        cached._kind &= ~AssemblyKind.Excluded;
                        monitor.Warn( $"PFeature definer assembly '{cached.Name}' is excluded. Ignoring the exclusion." );
                    }
                    // We don't analyze references of PFeatureDefiner: a definer is a "leaf", it says "Here starts the interesting parts".
                    // We analyze Engine references in Folder mode only to remove referenced PFeatures from the set of heads.
                    // We analyze PFeature references only if it is not excluded except in Folder mode: we don't wand to miss fake heads.
                    // We always analyze None (excluded or not) to know what it is.
                    bool mustHandleRefererences = cached._kind != AssemblyKind.AutoSkipped
                                                  &&
                                                  ((cached._kind.IsEngine() && !_explicitMode.Value)
                                                    || (cached._kind.IsPFeature() && (!cached._kind.IsExcluded() || !_explicitMode.Value))
                                                    || cached._kind.IsNone());
                    if( mustHandleRefererences )
                    {
                        HandleAssemblyReferences( monitor, cached );
                    }
                    else
                    {
                        cached._rawReferencedAssembly = ImmutableArray<CachedAssembly>.Empty;
                        monitor.Info( $"Ignoring '{cached.Name}' since it is '{cached.Kind}'. None of its referenced assemblies are analyzed." );
                    }
                }
            }

            if( cached.Kind.IsPFeature() )
            {
                foreach( var aRef in cached.PFeatures )
                {
                    // aRef is no more a head unless it has been explictly added.
                    if( _heads.TryGetValue( aRef, out var forced ) && !forced )
                    {
                        _heads.Remove( aRef );
                    }
                }
                // If no existing head references this cached assembly, it
                // is a head (preserve any explicit add).
                if( !_heads.Any( h => h.Key.AllPFeatures.Contains( cached ) ) )
                {
                    if( !_heads.TryGetValue( cached, out var forced ) && !forced )
                    {
                        _heads.Add( cached, false );
                    }
                }
            }

            return cached;
        }

        // Called by DoAdd and calls DoAdd on referenced assemblies.
        void HandleAssemblyReferences( IActivityMonitor monitor, CachedAssembly cached )
        {
            Throw.DebugAssert( _explicitMode.HasValue );
            // Resolves the CachedAssemblyReferences and detect the existence of an engine:
            // this assembly is an Engine as soon as it references an engine.
            var refNames = cached.Assembly.GetReferencedAssemblies();
            var rawRefBuilder = ImmutableArray.CreateBuilder<CachedAssembly>( refNames.Length );
            bool isEngine = cached.Kind.IsEngine();
            bool isPFeature = cached.Kind.IsPFeature();
            foreach( var name in refNames )
            {
                // Use the AssemblyName to load the references.
                var aRef = DoAdd( monitor, Assembly.Load( name ), name, null );
                rawRefBuilder.Add( aRef );
                if( aRef.Kind.IsEngine() )
                {
                    if( _explicitMode.Value && !isEngine )
                    {
                        // Emits a trace on the first dependency if none has been emitted so far.
                        // The final message for the root is done by Add.
                        monitor.Error( $"Assembly '{cached.Name}' is an engine because it references '{aRef.Name}'." );
                    }
                    isEngine = true;
                }
                isPFeature |= aRef.Kind.IsPFeatureOrDefiner();
            }
            cached._rawReferencedAssembly = rawRefBuilder.MoveToImmutable();
            // If it is an engine, sets its kind and forget any PFeature aspect.
            if( isEngine )
            {
                cached._kind = cached._kind.SetEngine();
                // An engine is not a PFeature.
                isPFeature = false;
            }
            // If this assembly is a PFeature, builds the 2 sets of dependencies:
            // the "all" one and the "curated" ones.
            HashSet<CachedAssembly>? allPFeatures = null;
            SortedSet<CachedAssembly>? pFeatures = null;
            if( isPFeature )
            {
                cached._kind = cached._kind.SetPFeature();
                allPFeatures = new HashSet<CachedAssembly>();
                pFeatures = cached._kind.IsExcluded() ? null : new SortedSet<CachedAssembly>();
                foreach( var aRef in cached._rawReferencedAssembly )
                {
                    if( aRef.Kind.IsPFeature() )
                    {
                        // Closure set:
                        allPFeatures.Add( aRef );
                        allPFeatures.AddRange( aRef._allPFeatures );
                        // Curated set: union all here, the [ExcludePFeature] attributes of this assembly
                        // will be applied below.
                        if( pFeatures != null && !aRef.Kind.IsExcluded() )
                        {
                            pFeatures.Add( aRef );
                            pFeatures.AddRange( aRef.PFeatures );
                        }
                    }
                }
            }
            // Always process any ExcludePFeature attributes even if this is not a PFeature
            // to warn on useless attributes.
            Throw.DebugAssert( cached._kind.IsNone() || cached._kind.IsEngine() || cached._kind.IsPFeature() );
            ProcessExcludePFeatures( monitor, cached, allPFeatures, pFeatures );
            // Types are handled later and only for PFeatures.
            // Here we just warn if a non PFeature defines [RegisterCKType] or [ExcludeCKType] attributes.
            if( !cached._kind.IsPFeature() )
            {
                // ExcludeCKTypeAttribute on Type is in CK.Core namespace, ExcludeCKTypeAttribute on Asssembly is in CK.Setup namespace.
                if( cached.CustomAttributes.Any( a => a.AttributeType == typeof( RegisterCKTypeAttribute ) || a.AttributeType == typeof( CK.Setup.ExcludeCKTypeAttribute ) ) )
                {
                    monitor.Warn( $"""
                              Assembly '{cached.Name}' is '{cached.Kind}' and defines [RegisterCKType] or [ExcludeCKType] attributes, they are ignored.
                              Only PFeature assemblies can register/exclude types.
                              """ );
                }
            }
            if( pFeatures != null )
            {
                Throw.DebugAssert( allPFeatures != null && cached._kind is AssemblyKind.PFeature );
                cached._pFeatures = pFeatures;
                cached._allPFeatures = allPFeatures;
            }
        }

        // Called by HandleAssemblyReferences.
        void ProcessExcludePFeatures( IActivityMonitor monitor,
                                      CachedAssembly cached,
                                      HashSet<CachedAssembly>? allPFeatures,
                                      SortedSet<CachedAssembly>? pFeatures )
        {
            // The excluded tuple keeps the "name" but can have a null (never seen) CachedAssembly.
            var excluded = cached.CustomAttributes.Where( a => a.AttributeType == typeof( ExcludePFeatureAttribute ) )
                                 .Select( a => (string?)a.ConstructorArguments[0].Value )
                                 .Select( name => (N: name, A: name != null ? _assemblyCache.Find( name ) : null) );
            foreach( var (name, a) in excluded )
            {
                if( a == null )
                {
                    monitor.Warn( $"Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': assembly '{name}' is unknwon." );
                }
                else if( pFeatures == null )
                {
                    if( cached._kind.IsPFeature() )
                    {
                        Throw.DebugAssert( cached._kind.IsExcluded() );
                        monitor.Trace( $"Ignored [assembly:ExcludePFeature( \"{name}\" )] in PFeature '{cached.Name}' since '{cached.Name}' is excluded." );
                    }
                    else
                    {
                        monitor.Warn( $"Ignored [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': this assembly is not a PFeature but '{cached.Kind}'." );
                    }
                }
                else if( pFeatures.Remove( a ) )
                {
                    // Success case: the curated set has been updated.
                    monitor.Info( $"PFeature '{cached.Name}' excludes its referenced assembly '{name}'. The types from '{name}' won't be automatically registered." );
                }
                else
                {
                    if( !a.Kind.IsPFeature() )
                    {
                        monitor.Warn( $"Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': '{name}' is not a PFeature but '{a.Kind}'." );
                    }
                    else
                    {
                        Throw.DebugAssert( "Because pFeature is not null.", allPFeatures != null );
                        if( !allPFeatures.Contains( a ) )
                        {
                            monitor.Warn( $"""
                                          Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': no such referenced PFeature. Referenced PFeatures are:
                                          '{allPFeatures.Select( r => r.Name ).Concatenate( "', '" )}'.
                                          """ );
                        }
                        else
                        {
                            monitor.Warn( $"""
                                          Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': the '{name}' assembly has already been excluded by the referenced assemblies:
                                          '{allPFeatures.Where( r => r._allPFeatures.Contains( a ) && !r.PFeatures.Contains( a ) ).Select( r => r.Name ).Concatenate( "', '" )}'.
                                          """ );
                        }
                    }
                }
            }
        }

        public override string ToString() => $"AssemblyBinPathGoup '{_groupName}'";
    }

}
