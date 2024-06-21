using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace CK.Engine.TypeCollector
{
    /// <summary>
    /// Collects assemblies, analyzing <see cref="IsPFeatureAttribute"/>, <see cref="IsPFeatureDefinerAttribute"/>,
    /// <see cref="ExcludePFeatureAttribute"/> and their referenced assemblies recursively.
    /// <para>
    /// Assemblies are considered as sets of types, they are used as a source for the type collector.
    /// </para>
    /// <para>
    /// This collector works in two exclusive ways:
    /// <list type="bullet">
    ///     <item>
    ///     Explicit mode: <see cref="AddExplicit(IActivityMonitor, string)"/> can be called as many times as there are
    ///     assemblies to handle. Each of the explicitly added assemblies are "heads": all their <see cref="CachedAssembly.AllVisibleTypes"/>
    ///     (plus their [RegisterCKType] minus their [ExcludeCKType]) must be considered.
    ///     Assemblies that are referenced by the "heads" must be treated differently: the set of types must be computed
    ///     by taking only their curated set of <see cref="CachedAssembly.PFeatures"/> and 
    ///     </item>
    ///     <item>
    ///     BinPath mode: <see cref="AddBinPath(IActivityMonitor)"/> must be called once and only once. The content of the <see cref="BinPath"/>
    ///     is analyzed and CKAssemblies that are not referenced by any CKEngines are the "heads". An edge case is when a folder has no "user asemblies",
    ///     all the CKAssemblies are "basic" assemblies that are managed by a CKEngine: there is no "head". This makes sense since there is no
    ///     "user assemblies" to process and this can be handled by explicitly adding the assemblies of interest instead of relying on this BinPath mode. 
    ///     </item>
    /// </list>
    /// Once assemblies are registered, <see cref="CloseRegistration(IActivityMonitor)"/> must be called to compute the set of types to register
    /// from the discovered heads. The resulting <see cref="TypeCollector"/> then continues to register assemblies when a new registered type
    /// belongs to an assembly that is not yet known.
    /// </para>
    /// </summary>
    public sealed class AssemblyCollector 
    {
        static NormalizedPath _baseDirectory = AppContext.BaseDirectory;

        // CachedAssembly are indexed by their Assembly and their simple name.
        readonly Dictionary<object, CachedAssembly> _assemblies;
        readonly NormalizedPath _binPath;
        readonly bool _isAppContextFolder;
        readonly Func<string, bool>? _configureExclude;
        // The head assemblies are mapped to true when "forced" to be a head.
        readonly Dictionary<CachedAssembly,bool> _heads;

        DateTime _maxFileTime = Util.UtcMinValue;
        readonly IncrementalHash _hasher;
        // Null when unknwon, true when ExplicitAdd is used, false for AddBinPath.
        bool? _explicitMode;
        // Success is set to false at the first error.
        bool _success;

        // Created by CloseRegistration.
        bool _closedRegistration;
        TypeCollector? _typeCollector;

        /// <summary>
        /// Initializes a new <see cref="AssemblyCollector"/>.
        /// </summary>
        /// <param name="binPath">The path that contains the assemblies.</param>
        /// <param name="configureExclude">Optional filter that can exclude an assembly when returning true.</param>
        public AssemblyCollector( NormalizedPath binPath, Func<string, bool>? configureExclude = null )
        {
            _assemblies = new Dictionary<object, CachedAssembly>();
            _binPath = binPath;
            _configureExclude = configureExclude;
            _heads = new Dictionary<CachedAssembly, bool>();
            _maxFileTime = Util.UtcMinValue;
            _hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );
            _isAppContextFolder = binPath == _baseDirectory;
            _success = true;
        }

        /// <summary>
        /// Post registration assemblies: called by the TypeCollector.
        /// </summary>
        /// <param name="assembly">The type's assembly to obtain.</param>
        /// <returns>The cached assembly.</returns>
        internal CachedAssembly EnsureAssembly( Assembly assembly )
        {
            Throw.DebugAssert( !assembly.IsDynamic );
            if( !_assemblies.TryGetValue( assembly, out var c ) )
            {
                GetAssemblyNames( assembly.GetName(), out var assemblyName, out var assemblyFullName );
                // AssemblyKind.None will trigger the detection of [IsPFeature], [IsPFeatureDefiner] and [IsCKEngine].
                c = DoCreateAndCache( assembly, assemblyName, assemblyFullName, AssemblyKind.None, null );
            }
            return c;
        }

        /// <summary>
        /// Gets whether no error occurred so far.
        /// </summary>
        public bool Success => _success;

        /// <summary>
        /// Gets the fully qualified folder path.
        /// </summary>
        public NormalizedPath BinPath => _binPath;

        /// <summary>
        /// Gets whether <see cref="BinPath"/> is <see cref="AppContext.BaseDirectory"/>.
        /// </summary>
        public bool IsAppContextFolder => _isAppContextFolder;

        /// <summary>
        /// Loads all assemblies found in a folder. This can be called only once and uses a very simple strategy:
        /// <list type="bullet">
        ///     <item>
        ///     Only "*.dll" files are considered.
        ///     </item>
        ///     <item>
        ///     Only their file name matters: the same assembly file name must be available in the <see cref="AppContext.BaseDirectory"/>.
        ///     </item>
        ///     <item>
        ///     When <see cref="IsAppContextFolder"/> is false, the <see cref="File.GetLastWriteTimeUtc(string)"/>
        ///     of the <see cref="BinPath"/>'s file must not be newer than the BaseDirectory's one.
        ///     </item>
        ///     <item>
        ///     Assemblies are loaded in the <see cref="AssemblyLoadContext.Default"/>, there is no sandbox or any assembly context involved.
        ///     </item>
        ///     <item>
        ///     Assembly loading error are only warnings, not errors.
        ///     </item>
        /// </list>
        /// Any error definitely sets <see cref="Success"/> to false.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The <see cref="Success"/> flag.</returns>
        public bool AddBinPath( IActivityMonitor monitor )
        {
            Throw.CheckArgument( Path.IsPathFullyQualified( _binPath ) );
            Throw.CheckState( "AddBinPath can be called only once.", _explicitMode is null );
            Throw.CheckState( "CloseRegistration already called.", _closedRegistration is false );
            _explicitMode = false;

            foreach( var f in Directory.EnumerateFiles( _binPath, "*.dll" ) )
            {
                LoadAssembly( monitor, f, out var _ );
            }
            return _success;
        }

        /// <summary>
        /// Adds an assembly. It is an error to:
        /// <list type="bullet">
        /// <item>
        /// Add an assembly that doesn't exist in the <paramref name="BinPath"/>.
        /// </item>
        /// <item>
        /// Add an assembly that doesn't exist in the <see cref="AppContext.BaseDirectory"/>.
        /// </item>
        /// <item>
        /// Add an assembly whose <see cref="File.GetLastWriteTimeUtc(string)"/> in <paramref name="BinPath"/> is newer than the one in <see cref="AppContext.BaseDirectory"/>.
        /// </item>
        /// <item>
        /// Add an engine assembly (any assembly that depends on a engine is an engine).
        /// </item>
        /// </list>
        /// Any error definitely sets <see cref="Success"/> to false.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="assemblyName">The assembly from which public types must be collected.</param>
        /// <returns>The <see cref="Success"/> flag.</returns>
        public bool AddExplicit( IActivityMonitor monitor, string assemblyName )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( assemblyName );
            Throw.CheckArgument( !assemblyName.EndsWith( "*.dll", StringComparison.OrdinalIgnoreCase ) );
            Throw.CheckState( "AddBinPath has been called.", _explicitMode is null or true );
            Throw.CheckState( "CloseRegistration already called.", _closedRegistration is false );
            _explicitMode = true;

            var existingFilePath = _binPath.AppendPart( assemblyName + ".dll" );
            if( !File.Exists( existingFilePath ) )
            {
                monitor.Error( $"Failed to register assembly: file '{assemblyName}.dll' not found in the bin path '{_binPath}'." );
                return _success = false;
            }
            if( LoadAssembly( monitor, existingFilePath, out var cachedAssembly ) )
            {
                Throw.DebugAssert( cachedAssembly != null );
                // Explictly adding an engine is a configuration error.
                if( cachedAssembly.Kind == AssemblyKind.CKEngine )
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

        /// <summary>
        /// Get the current set of "head" assemblies.
        /// </summary>
        /// <returns>The set of "head" assemblies.</returns>
        public IReadOnlyCollection<CachedAssembly> HeadAssemblies => _heads.Keys;

        /// <summary>
        /// Closes the work on the assemblies. On success, the <see cref="TypeCollector"/> takes the lead from now on.
        /// <para>
        /// When called multiple times, the same TypeCollector is returned (always null if the first call failed).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>The type collector or null on error.</returns>
        public TypeCollector? CloseRegistration( IActivityMonitor monitor )
        {
            if( _closedRegistration ) return _typeCollector;
            _closedRegistration = true;
            // 99% of the types will be registered from the CachedAssembly.AllVisibleTypes,
            // so we can avoid 99% of lookup to find the CachedAssembly from the Type.Assembly.
            var c = new Dictionary<Type, CachedAssembly?>();
            bool success = true;
            foreach( var h in _heads.Keys )
            {
                success &= CollectTypes( monitor, h, c );
            }
            return _typeCollector = success ? TypeCollector.Create( monitor, this, c ) : null;

            static bool CollectTypes( IActivityMonitor monitor, CachedAssembly assembly, Dictionary<Type,CachedAssembly?> c )
            {
                bool success = true;
                foreach( var sub in assembly.PFeatures )
                {
                    success &= CollectTypes( monitor, sub, c );
                }
                foreach( var t in assembly.AllVisibleTypes )
                {
                    c.Add( t, assembly );
                }
                // Don't merge the 2 loops here!
                // We must first handle the Add and then the Remove.
                // 1 - Add types.
                List<Type>? changed = null;
                foreach( var a in assembly.CustomAttributes )
                {
                    if( a.AttributeType == typeof( RegisterCKTypeAttribute ) )
                    {
                        var ctorArgs = a.ConstructorArguments;
                        // Filters out null thanks to is.
                        if( ctorArgs[0].Value is Type t )
                        {
                            success &= HandleType( monitor, c, ref changed, true, t );
                        }
                        // Maximal precautions: filters out any potential null.
                        if( ctorArgs[1].Value is Type?[] others && others.Length > 0 )
                        {
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleType( monitor, c, ref changed, true, o );
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
                            success &= HandleType( monitor, c, ref changed, false, t );
                        }
                        if( ctorArgs[1].Value is Type?[] others && others.Length > 0 )
                        {
                            foreach( var o in others )
                            {
                                if( o == null ) continue;
                                success &= HandleType( monitor, c, ref changed, false, o );
                            }
                        }
                    }
                }
                if( success && changed != null )
                {
                    monitor.Info( $"Assembly '{assembly.Name}' explicitly removed {changed.Count} types from registration: '{changed.Select( t => t.ToCSharpName() ).Concatenate( "', '" )}'." );
                }
                return success;

                static bool HandleType( IActivityMonitor monitor, Dictionary<Type, CachedAssembly?> c, ref List<Type>? changed, bool add, Type t )
                {
                    var invalid = TypeCollector.GetTypeInvalidity( t, false );
                    if( invalid != null )
                    {
                        monitor.Error( $"Invalid [assembly:{(add ? "Register" : "Exclude")}CKTypeAttribute( typeof({t:N}) )]: type {invalid}." );
                        return false;
                    }
                    if( add ? c.TryAdd( t, null ) : c.Remove( t ) )
                    {
                        changed ??= new List<Type>();
                        changed.Add( t );
                    }
                    return true;
                }
            }
        }

        // For AddBinPath an assembly load error is just a warning. In this mode this may return true with a
        // null out result (and we don't care of the result in this mode).
        // In Explicit mode, a true return implies a non null out result.
        //
        // > Called by AddExplicit and AddBinPath.
        bool LoadAssembly( IActivityMonitor monitor, NormalizedPath existingFilePath, out CachedAssembly? result )
        {
            Throw.DebugAssert( _explicitMode.HasValue );
            if( HandleFile( monitor, existingFilePath, out NormalizedPath appContextFullPath, out DateTime existingFileTime ) )
            {
                try
                {
                    var a = AssemblyLoadContext.Default.LoadFromAssemblyPath( appContextFullPath );
                    result = DoAdd( monitor, a, a.GetName(), existingFileTime );
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
                    monitor.Warn( $"Failed to load '{existingFilePath.LastPart}' from '{_baseDirectory}'. Ignored.", ex );
                    result = null;
                    return true;
                }
            }
            Throw.DebugAssert( !_success );
            result = null;
            return false;
        }

        // Called by LoadAssembly once the assembly has been loaded.
        // On success, LoadAssembly calls DoAdd.
        bool HandleFile( IActivityMonitor monitor, NormalizedPath existingFile, out NormalizedPath appContextFullPath, out DateTime existingFileTime )
        {
            existingFileTime = File.GetLastWriteTimeUtc( existingFile );
            if( _isAppContextFolder )
            {
                appContextFullPath = existingFile;
            }
            else
            {
                appContextFullPath = _baseDirectory.AppendPart( existingFile.LastPart );
                if( !File.Exists( appContextFullPath ) )
                {
                    monitor.Error( $"Assembly cannot be registered: file '{existingFile.LastPart}.dll' is not in AppContext.BaseDirectory ({_baseDirectory})." );
                    return false;
                }
                var appContextFileTime = File.GetLastWriteTimeUtc( appContextFullPath );
                if( existingFileTime > appContextFileTime )
                {
                    monitor.Error( $"File '{existingFile.LastPart}' is more recent in '{_binPath}' than in the AppContext.BaseDirectory '{_baseDirectory}'." );
                    return _success = false;
                }
            }
            if( _maxFileTime < existingFileTime ) _maxFileTime = existingFileTime;
            return true;
        }

        // Called by LoadAssembly and recursively by HandleAssemblyReferences.
        // Calls HandleAssemblyReferences.
        CachedAssembly DoAdd( IActivityMonitor monitor,
                              Assembly assembly,
                              AssemblyName aName,
                              DateTime? knownLastWriteTime )
        {
            Throw.DebugAssert( _explicitMode != null );

            if( !_assemblies.TryGetValue( assembly, out var cached ) )
            {
                GetAssemblyNames( aName, out string assemblyName, out string assemblyFullName );
                AssemblyKind initialKind = AssemblyKind.None;
                if( IsSkipped( assemblyName ) )
                {
                    initialKind = AssemblyKind.Skipped;
                }
                else if( _configureExclude != null && _configureExclude( assemblyName ) )
                {
                    initialKind = AssemblyKind.Excluded;
                }
                cached = DoCreateAndCache( assembly, assemblyName, assemblyFullName, initialKind, knownLastWriteTime );

                Throw.DebugAssert( cached.Name == assemblyName );
                // The cached.Kind Excluded can come from Auto Exclusion via the [ExcludePAssembly( "this" )].
                bool mustHandleRefererences = initialKind is AssemblyKind.None && cached.Kind is not AssemblyKind.Excluded;
                if( mustHandleRefererences )
                {
                    HandleAssemblyReferences( monitor, cached );
                }
                else
                {
                    var reason = cached.Kind is AssemblyKind.Excluded ? "Auto excluded" : initialKind.ToString();
                    monitor.Info( $"Ignoring '{assemblyName}' since it is '{reason}'. None of its referenced assemblies are analyzed." );
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
            bool isEngine = cached.Kind is AssemblyKind.CKEngine;
            bool isPFeature = cached.Kind is AssemblyKind.PFeature;
            foreach( var name in refNames )
            {
                // Use the AssemblyName to load the references.
                var aRef = DoAdd( monitor, Assembly.Load( name ), name, null );
                rawRefBuilder.Add( aRef );
                if( aRef.Kind == AssemblyKind.CKEngine )
                {
                    if( _explicitMode.Value && !isEngine )
                    {
                        // Emits a trace on the first dependency if none has been emitted so far.
                        // The final message for the root is done by Add.
                        monitor.Error( $"Assembly '{cached.Name}' is an engine because it references '{aRef.Name}'." );
                    }
                    isEngine = true;
                }
                isPFeature |= aRef.Kind >= AssemblyKind.PFeatureDefiner;
            }
            cached._rawReferencedAssembly = rawRefBuilder.MoveToImmutable();
            // If it is an engine, sets its kind and prevents any referenced PFeature
            // to be a head unless it has been expcitly added.
            if( isEngine )
            {
                cached._kind = AssemblyKind.CKEngine;
                // An engine is not a PFeature.
                isPFeature = false;
                foreach( var aRef in cached._rawReferencedAssembly )
                {
                    if( aRef.Kind == AssemblyKind.PFeature
                        && _heads.TryGetValue( aRef, out var forced )
                        && !forced )
                    {
                        _heads.Remove( aRef );
                    }
                }
            }
            // If this assembly is a PFeature, builds the 2 sets of dependencies:
            // the "all" one and the "curated" ones.
            HashSet<CachedAssembly>? allPFeatures = null;
            HashSet<CachedAssembly>? pFeatures = null;
            if( isPFeature )
            {
                Throw.DebugAssert( cached.Kind is AssemblyKind.None );
                cached._kind = AssemblyKind.PFeature;
                allPFeatures = new HashSet<CachedAssembly>();
                pFeatures = new HashSet<CachedAssembly>();
                foreach( var aRef in cached._rawReferencedAssembly )
                {
                    if( aRef.Kind == AssemblyKind.PFeature )
                    {
                        // aRef is no more a head unless it has been explictly added.
                        if( _heads.TryGetValue( aRef, out var forced ) && !forced )
                        {
                            _heads.Remove( aRef );
                        }
                        // Closure set:
                        allPFeatures.Add( aRef );
                        allPFeatures.AddRange( aRef._allPFeatures );
                        // Curated set: union all here, the [ExcludePFeatureAttribute] of this assembly
                        // will be applied below.
                        pFeatures.Add( aRef );
                        pFeatures.AddRange( aRef.PFeatures );
                    }
                }
            }
            // Always process any ExcludePFeature attributes even if this is not a PFeature
            // to warn on useless attributes.
            Throw.DebugAssert( cached.Kind is AssemblyKind.None or AssemblyKind.CKEngine or AssemblyKind.PFeature );
            ProcessExcludePFeatures( monitor, cached, allPFeatures, pFeatures );
            // Types are handled later and only for PFeatures.
            // Here we just warn if a non PFeature defines [RegisterCKType] or [ExcludeCKType] attributes.
            if( cached.Kind is not AssemblyKind.PFeature )
            {
                if( cached.CustomAttributes.Any( a => a.AttributeType == typeof( RegisterCKTypeAttribute ) || a.AttributeType == typeof( ExcludePFeatureAttribute) ) )
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
                _heads.Add( cached, false );
            }
        }

        // Called by HandleAssemblyReferences.
        void ProcessExcludePFeatures( IActivityMonitor monitor,
                                       CachedAssembly cached,
                                       HashSet<CachedAssembly>? allPFeatures,
                                       HashSet<CachedAssembly>? pFeatures )
        {
            // The excluded tuple keeps the "name" but can have a null (never seen) CachedAssembly.
            var excluded = cached.CustomAttributes.Where( a => a.AttributeType == typeof( ExcludePFeatureAttribute ) )
                                 .Select( a => (string?)a.ConstructorArguments[0].Value )
                                 .Select( name => (N: name, A: name != null ? _assemblies.GetValueOrDefault( name ) : null) );
            foreach( var (name, a) in excluded )
            {
                if( pFeatures == null )
                {
                    monitor.Warn( $"Ignored [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': this assembly is not a PFeature but '{cached.Kind}'." );
                }
                else if( a == null )
                {
                    monitor.Warn( $"Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': assembly '{name}' is unknwon." );
                }
                else if( pFeatures.Remove( a ) )
                {
                    // Success case: the curated set has been updated.
                    monitor.Info( $"PFeature '{cached.Name}' excludes its referenced assembly '{name}'. The types from '{name}' won't be automatically registered." );
                }
                else
                {
                    if( a.Kind != AssemblyKind.PFeature )
                    {
                        // Warn only if it's not an Excluded or Skipped assembly.
                        if( a.Kind is not AssemblyKind.Skipped or AssemblyKind.Excluded )
                        {
                            monitor.Warn( $"Useless [assembly:ExcludePFeature( \"{name}\" )] in assembly '{cached.Name}': '{name}' is not a PFeature but '{a.Kind}'." );
                        }
                    }
                    else
                    {
                        Throw.DebugAssert( allPFeatures != null );
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

        static void GetAssemblyNames( AssemblyName aName, out string assemblyName, out string assemblyFullName )
        {
            assemblyName = aName.Name!;
            assemblyFullName = aName.FullName;
            if( string.IsNullOrWhiteSpace( assemblyName ) || string.IsNullOrWhiteSpace( assemblyFullName ) )
            {
                Throw.ArgumentException( "Invalid assembly: the AssemmblyName.Name or assemblyName.FullName is null, empty or whitespace." );
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
                var tryPath = $"{_baseDirectory}{assemblyName}.dll";
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
            _hasher.AppendData( MemoryMarshal.Cast<char,byte>( assemblyName.AsSpan() ) );
            _hasher.AppendData( MemoryMarshal.AsBytes( MemoryMarshal.CreateReadOnlySpan( ref lastWriteTime, 1 ) ) );
            return cached;
        }

        static bool IsSkipped( string assemblyName )
        {
            return assemblyName.StartsWith( "Microsoft." )
                   || assemblyName.StartsWith( "System." );
        }

    }
}
