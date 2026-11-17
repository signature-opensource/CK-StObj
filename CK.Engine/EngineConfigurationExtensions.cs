
using CK.Core;
using CK.TypeCache;
using CK.TypeSet;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace CK.Setup;

/// <summary>
/// The CKomposable engine entry point.
/// </summary>
public static class CKEngine
{
    /// <summary>
    /// Runs this <see cref="EngineConfiguration"/>.
    /// This configuration is first normalized and should have no error: if <see cref="EngineConfiguration.NormalizeConfiguration(IActivityMonitor)"/>
    /// fails, a null result is returned, otherwise a non null result is always returned (<see cref="EngineResult.Status"/> may be <see cref="RunStatus.Failed"/>).
    /// </summary>
    /// <param name="configuration">This configuration.</param>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="assemblies">
    /// The result of a previous analysis of the assemblies of this configuration (the App builder computes the type sets
    /// of its Apps before running the engine): reused when the BinPaths didn't change.
    /// </param>
    /// <returns>A <see cref="EngineResult"/> or null if this configuration is invalid.</returns>
    public static Task<EngineResult?> RunAsync( this EngineConfiguration configuration, IActivityMonitor monitor, AssemblyCache.Result? assemblies = null )
    {
        var typeGroups = BinPathTypeGroup.Run( monitor, configuration, assemblies );
        if( typeGroups == null )
        {
            return Task.FromResult<EngineResult?>( null );
        }
        var groups = typeGroups.Groups.Select( tG => new BinPathGroup( tG ) ).ToImmutableArray();
        if( typeGroups.Success && CheckRequiredEngines( monitor, typeGroups.AssemblyCache ) )
        {
            // Temporary use of the good old StObjEngine.
            var engine = new StObjEngine( monitor, configuration, typeGroups.TypeCache, typeGroups.Groups );
            var r = engine.NewRun();
            for( int i = 0; i < r.Groups.Count; i++ )
            {
                groups[i]._runningGroup = r.Groups[i];
            }
            return Task.FromResult<EngineResult?>( new EngineResult( r.Success ? RunStatus.Succeed : RunStatus.Failed, configuration, groups ) );
        }
        else
        {
            return Task.FromResult<EngineResult?>( new EngineResult( RunStatus.Failed, configuration, groups ) );
        }
    }

    /// <summary>
    /// Checks that the engines that the models require (their <see cref="RequiredEngineAttribute"/>, and the ones that
    /// these engines require) can be loaded: the missing ones are reported together, before the run.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="assemblies">The analyzed assemblies.</param>
    /// <returns>True when every required engine can be loaded.</returns>
    public static bool CheckRequiredEngines( IActivityMonitor monitor, AssemblyCache assemblies )
    {
        // The required engine name -> the assemblies that require it.
        var required = new Dictionary<string, List<string>>( StringComparer.OrdinalIgnoreCase );
        foreach( var a in assemblies.Assemblies.Values.Distinct() )
        {
            if( !a.Kind.IsModelOrDefiner() ) continue;
            foreach( var (name, _) in a.RequiredEngines ) Add( required, name, a.Name );
        }
        var missing = new List<string>();
        var done = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        var todo = new Queue<string>( required.Keys );
        while( todo.TryDequeue( out var name ) )
        {
            if( !done.Add( name ) ) continue;
            Assembly engine;
            try
            {
                engine = AssemblyLoadContext.Default.LoadFromAssemblyName( new AssemblyName( name ) );
            }
            catch( FileNotFoundException )
            {
                missing.Add( name );
                continue;
            }
            // An engine can require other engines.
            foreach( var d in engine.GetCustomAttributesData() )
            {
                if( d.AttributeType.Name == nameof( RequiredEngineAttribute )
                    && d.ConstructorArguments.Count >= 1
                    && d.ConstructorArguments[0].Value is string sub )
                {
                    Add( required, sub, name );
                    todo.Enqueue( sub );
                }
            }
        }
        if( missing.Count == 0 ) return true;
        monitor.Error( $"The models require {missing.Count} engine(s) that can't be loaded: the builder (or the test project) must reference them:{Environment.NewLine}"
                       + missing.Order( StringComparer.OrdinalIgnoreCase )
                                .Select( m => $"- '{m}', required by '{required[m].Concatenate( "', '" )}'." )
                                .Concatenate( Environment.NewLine ) );
        return false;

        static void Add( Dictionary<string, List<string>> required, string name, string by )
        {
            if( !required.TryGetValue( name, out var l ) ) required.Add( name, l = new List<string>() );
            if( !l.Contains( by, StringComparer.OrdinalIgnoreCase ) ) l.Add( by );
        }
    }
}
