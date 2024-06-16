using CK.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CK.Setup
{

    /// <summary>
    /// Collects assemblies, analyzing <see cref="IsModelAttribute"/>, <see cref="IsModelDependentAttribute"/>,
    /// <see cref="ExcludeCKAssemblyAttribute"/> and their referenced assemblies recursively.
    /// </summary>
    public sealed class AssemblyCollector 
    {
        // CachedAssembly are indexed by their Assembly and their simple name.
        readonly Dictionary<object, CachedAssembly> _assemblies;
        readonly Func<Assembly, AssemblyName, bool>? _configureExclude;
        readonly Dictionary<CachedAssembly,bool> _heads;
        bool _hasError;

        /// <summary>
        /// Initializes a new <see cref="AssemblyCollector"/>.
        /// </summary>
        /// <param name="configureExclude">Optional filter that can exclude an assembly when returning true.</param>
        public AssemblyCollector( Func<Assembly, AssemblyName, bool>? configureExclude = null )
        {
            _assemblies = new Dictionary<object, CachedAssembly>();
            _configureExclude = configureExclude;
            _heads = new Dictionary<CachedAssembly, bool>();
        }

        /// <summary>
        /// Gets whether at least one error occurred so far.
        /// </summary>
        public bool HasError => _hasError;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="monitor">The </param>
        /// <param name="folderPath">The fully qualified path of the directory to process.</param>
        /// <returns>The <see cref="HasError"/> flag.</returns>
        public bool AddFromBinPath( IActivityMonitor monitor, string folderPath )
        {
            Throw.CheckArgument( folderPath != null && Path.IsPathFullyQualified( folderPath ) );
            foreach( var f in Directory.EnumerateFiles( folderPath ) )
            {
                if( f.EndsWith( ".dll" ) || f.EndsWith( "*.exe" ) )
                {
                    var fName = Path.GetFileName( f );
                    try
                    {
                        var local = Path.Combine( AppContext.BaseDirectory, fName );
                        var a = AssemblyLoadContext.Default.LoadFromAssemblyPath( local );
                        DoAdd( monitor, a, a.GetName(), allowEngine: true );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( $"Failed to load '{fName}' from '{AppContext.BaseDirectory}'.", ex );
                        _hasError = true;
                    }
                }
            }
            return !_hasError;
        }

        /// <summary>
        /// Adds an assembly. It is an error to:
        /// <list type="bullet">
        /// <item>Add an engine assembly (any assembly that depends on a engine is an engine).</item>
        /// <item>Setting <paramref name="forceInclude"/> to true on an assembly that is not a <see cref="CKAssemblyKind.CKAssembly"/>.</item>
        /// </list>
        /// </summary>
        /// <param name="assembly">The root assembly from which public types must be collected.</param>
        /// <param name="forceInclude">True to ignore any [ExcludeCKAssembly] from referencing assemblies.</param>
        /// <returns>The <see cref="HasError"/> flag.</returns>
        public bool Add( IActivityMonitor monitor, Assembly assembly, bool forceInclude = false )
        {
            var a = DoAdd( monitor, assembly, assembly.GetName(), allowEngine: false );
            if( a.Kind == CKAssemblyKind.CKEngine )
            {
                monitor.Error( $"Assembly '{a.Name}' is a CKEngine. Engine assemblies cannot be added to an AssemblyCollector." );
                _hasError = true;
            }
            else if( forceInclude )
            {
                if( a.Kind != CKAssemblyKind.CKAssembly )
                {
                    monitor.Error( $"Assembly '{a.Name}' is not a CKAssembly. Forcing its inclusion doesn't make sense." );
                    _hasError = true;
                }
                else
                {
                    _heads[a] = true;
                }
            }
            return !_hasError;
        }

        /// <summary>
        /// Computes the final set of assemblies to consider.
        /// </summary>
        /// <returns>The set of assemblies to process.</returns>
        public HashSet<CachedAssembly> GetFinalAssemblies()
        {
            var result = new HashSet<CachedAssembly>();
            foreach( var a in _heads.Keys )
            {
                result.Add( a );
                result.AddRange( a.CKAssemblies );
            }
            return result;
        }

        /// <summary>
        /// Adds the final set of assemblies names to the collector.
        /// </summary>
        /// <param name="assemblyNames">The assembly name collector.</param>
        /// <param name="useSimpleNames">False to use <see cref="AssemblyName.FullName"/> instead of <see cref="AssemblyName.Name"/>.</param>
        /// <returns>The number of assembly names added.</returns>
        public int AddFinalAssemblyNames( ISet<string> assemblyNames, bool useSimpleNames = true )
        {
            int added = 0;
            foreach( var a in _heads.Keys )
            {
                if( assemblyNames.Add( useSimpleNames ? a.Name : a.FullName ) ) ++added;
                foreach( var d in a.CKAssemblies )
                {
                    if( assemblyNames.Add( useSimpleNames ? d.Name : d.FullName ) ) ++added;
                }
            }
            return added;
        }

        CachedAssembly DoAdd( IActivityMonitor monitor, Assembly assembly, AssemblyName assemblyName, bool allowEngine )
        {
            if( string.IsNullOrWhiteSpace( assemblyName.Name ) || string.IsNullOrWhiteSpace( assemblyName.FullName ) )
            {
                Throw.ArgumentException( "Invalid assembly: the AssemmblyName.Name or assemblyName.FullName is null, empty or whitespace." );
            }
            if( !_assemblies.TryGetValue( assembly, out var cached ) )
            {
                CKAssemblyKind initialKind = CKAssemblyKind.None;
                if( IsSkipped( assembly, assemblyName ) )
                {
                    initialKind = CKAssemblyKind.Skipped;
                }
                else if( _configureExclude != null && _configureExclude( assembly, assemblyName ) )
                {
                    initialKind = CKAssemblyKind.Excluded;
                }
                cached = new CachedAssembly( assembly, assemblyName, initialKind );
                _assemblies.Add( assembly, cached );
                if( _assemblies.ContainsKey( assemblyName.Name ) )
                {
                    Throw.InvalidOperationException( $"Duplicate assembly name '{assemblyName.Name}': an assembly with this name has already been registered." );
                }
                _assemblies.Add( assemblyName.Name, cached );
                if( initialKind is CKAssemblyKind.None )
                {
                    // Resolves the CachedAssemblyReferences and detect the existence of an engine:
                    // this assembly is an Engine as soon as it references an engine.
                    var refNames = assembly.GetReferencedAssemblies();
                    var rawRefBuilder = ImmutableArray.CreateBuilder<CachedAssembly>( refNames.Length );
                    bool isEngine = cached.Kind is CKAssemblyKind.CKEngine;
                    bool isCKAssembly = cached.Kind is CKAssemblyKind.CKAssembly;
                    foreach( var name in refNames )
                    {
                        var aRef = DoAdd( monitor, Assembly.Load( name ), name, allowEngine );
                        rawRefBuilder.Add( aRef );
                        if( aRef.Kind == CKAssemblyKind.CKEngine )
                        {
                            if( !allowEngine && !isEngine )
                            {
                                // Emits a trace on the first dependency if none has been emitted so far.
                                // The final message for the root is done by Add.
                                monitor.Error( $"Assembly '{assemblyName.Name}' is an engine because it references '{aRef.Name}'." );
                            }
                            isEngine = true;
                        }
                        isCKAssembly |= aRef.Kind >= CKAssemblyKind.CKAssemblyDefiner;
                    }
                    cached._rawReferencedAssembly = rawRefBuilder.MoveToImmutable();
                    // If it is an engine, sets its kind and prevents any referenced CKAssembly
                    // to be a head unless it has been expcitly added.
                    if( isEngine )
                    {
                        cached._kind = CKAssemblyKind.CKEngine;
                        // An engine is not a CKAssembly.
                        isCKAssembly = false;
                        foreach( var aRef in cached._rawReferencedAssembly )
                        {
                            if( aRef.Kind == CKAssemblyKind.CKAssembly
                                && _heads.TryGetValue( aRef, out var forced )
                                && !forced )
                            {
                                _heads.Remove( aRef );
                            }
                        }
                    }
                    // If this assembly is a CKAssembly, builds its set of dependencies.
                    HashSet<CachedAssembly>? ckAssemblies = null;
                    if( isCKAssembly )
                    {
                        ckAssemblies = new HashSet<CachedAssembly>();
                        foreach( var aRef in cached._rawReferencedAssembly )
                        {
                            if( aRef.Kind == CKAssemblyKind.CKAssembly )
                            {
                                if( _heads.TryGetValue( aRef, out var forced ) && !forced )
                                {
                                    _heads.Remove( aRef );
                                }
                                ckAssemblies.Add( aRef );
                                ckAssemblies.AddRange( aRef.CKAssemblies );
                            }
                        }
                    }
                    // Always process any ExcludeCKAssembly attributes.
                    var hiddenNames = cached.CustomAttributes.Where( a => a.AttributeType == typeof( ExcludeCKAssemblyAttribute ) )
                                         .Select( a => (string?)a.ConstructorArguments[0].Value )
                                         .Select( name => (N: name, A: name != null ? _assemblies.GetValueOrDefault( name ) : null) );
                    foreach( var (name, a) in hiddenNames )
                    {
                        if( ckAssemblies == null )
                        {
                            monitor.Warn( $"Useless [assembly:ExcludeCKAssembly( \"{name}\" )] in assembly '{assemblyName.Name}': this assembly is not a CKAssembly." );
                        }
                        else if( a != null && ckAssemblies.Remove( a ) )
                        {
                            monitor.Info( $"Assembly '{assemblyName.Name}' defines [assembly:ExcludeCKAssembly( \"{name}\" )]: removed referenced assemblies." );
                        }
                        else if( a == null || !cached._rawReferencedAssembly.Contains( a ) )
                        { 
                            monitor.Error( $"Invalid [assembly:ExcludeCKAssembly( \"{name}\" )] in assembly '{assemblyName.Name}': no referenced assembly with that name exists." );
                            _hasError = true;
                        }
                        else
                        {
                            monitor.Warn( $"Useless [assembly:ExcludeCKAssembly( \"{name}\" )] in assembly '{assemblyName.Name}': the '{name}' assembly is not a CKAssembly." );
                        }
                    }
                    if( ckAssemblies != null )
                    {
                        cached._kind = CKAssemblyKind.CKAssembly;
                        cached._ckAssemblies = ckAssemblies;
                        _heads.Add( cached, false );
                    }
                }
            }
            return cached;
        }

        static bool IsSkipped( Assembly assembly, AssemblyName assemblyName )
        {
            var n = assemblyName.Name;
            return n == null
                   || n.StartsWith( "Microsoft." )
                   || n.StartsWith( "System." );
        }
    }
}
