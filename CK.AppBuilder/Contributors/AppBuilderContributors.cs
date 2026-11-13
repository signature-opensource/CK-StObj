using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace CK.Setup;

/// <summary>
/// Finds the <see cref="IAppBuilderContributor"/> declared by the assemblies of the builder's folder
/// (<see cref="AppBuilderContributorAttribute"/>), creates them in the order of their constructor parameters (a parameter
/// is a requirement) and runs them.
/// </summary>
static class AppBuilderContributors
{
    public static bool Run( IActivityMonitor monitor, AppBuilderContext context, string folder )
    {
        using var _ = monitor.OpenInfo( "Running the App builder contributors." );
        var types = Discover( monitor, folder );
        if( types == null ) return false;
        var ordered = new List<IAppBuilderContributor>();
        var created = new Dictionary<Type, IAppBuilderContributor>();
        var visiting = new Stack<Type>();
        foreach( var t in types )
        {
            if( Create( monitor, t, types, created, visiting, ordered ) == null ) return false;
        }
        monitor.Info( ordered.Count == 0
                        ? "No contributor."
                        : $"{ordered.Count} contributor(s): '{ordered.Select( c => c.GetType().ToCSharpName() ).Concatenate( "', '" )}'." );
        bool success = true;
        foreach( var c in ordered )
        {
            using( monitor.OpenTrace( $"Contribute: '{c.GetType().ToCSharpName()}'." ) )
            {
                success &= Call( monitor, c, () => c.Contribute( monitor, context ) );
            }
        }
        if( !success ) return false;
        foreach( var c in ordered )
        {
            success &= Call( monitor, c, () => c.OnContributed( monitor, context ) );
        }
        return success;

        static bool Call( IActivityMonitor monitor, IAppBuilderContributor c, Func<bool> f )
        {
            try
            {
                return f();
            }
            catch( Exception ex )
            {
                monitor.Error( $"Contributor '{c.GetType().ToCSharpName()}' failed.", ex );
                return false;
            }
        }
    }

    static IReadOnlyList<Type>? Discover( IActivityMonitor monitor, string folder )
    {
        var types = new List<Type>();
        bool success = true;
        foreach( var file in Directory.EnumerateFiles( folder, "*.dll" ).Order( StringComparer.OrdinalIgnoreCase ) )
        {
            var fileName = Path.GetFileName( file );
            // The framework and the third party libraries don't declare contributors.
            if( fileName.StartsWith( "System.", StringComparison.Ordinal )
                || fileName.StartsWith( "Microsoft.", StringComparison.Ordinal ) )
            {
                continue;
            }
            Assembly a;
            try
            {
                a = AssemblyLoadContext.Default.LoadFromAssemblyName( AssemblyName.GetAssemblyName( file ) );
            }
            catch( BadImageFormatException )
            {
                // A native library.
                continue;
            }
            catch( Exception ex )
            {
                monitor.Warn( $"Unable to load '{file}': its contributors (if any) are ignored.", ex );
                continue;
            }
            foreach( var attr in a.GetCustomAttributes<AppBuilderContributorAttribute>() )
            {
                var t = attr.ContributorType;
                if( !typeof( IAppBuilderContributor ).IsAssignableFrom( t ) || t.IsAbstract )
                {
                    monitor.Error( $"[AppBuilderContributor( typeof( {t.ToCSharpName()} ) )] of '{a.GetName().Name}': the type must be a concrete {nameof( IAppBuilderContributor )}." );
                    success = false;
                }
                else if( !types.Contains( t ) ) types.Add( t );
            }
        }
        return success ? types : null;
    }

    static IAppBuilderContributor? Create( IActivityMonitor monitor,
                                           Type t,
                                           IReadOnlyList<Type> types,
                                           Dictionary<Type, IAppBuilderContributor> created,
                                           Stack<Type> visiting,
                                           List<IAppBuilderContributor> ordered )
    {
        if( created.TryGetValue( t, out var c ) ) return c;
        if( visiting.Contains( t ) )
        {
            monitor.Error( $"Cycle between the App builder contributors: '{visiting.Reverse().Append( t ).Select( v => v.ToCSharpName() ).Concatenate( "' -> '" )}'." );
            return null;
        }
        var ctors = t.GetConstructors();
        if( ctors.Length != 1 )
        {
            monitor.Error( $"The App builder contributor '{t.ToCSharpName()}' must have exactly one public constructor (its parameters are the contributors it requires)." );
            return null;
        }
        visiting.Push( t );
        var parameters = ctors[0].GetParameters();
        var args = new object[parameters.Length];
        for( int i = 0; i < parameters.Length; ++i )
        {
            var pType = parameters[i].ParameterType;
            var required = types.FirstOrDefault( pType.IsAssignableFrom );
            if( required == null )
            {
                monitor.Error( $"The App builder contributor '{t.ToCSharpName()}' requires '{pType.ToCSharpName()}' that is not a contributor of the builder (a missing package reference?)." );
                visiting.Pop();
                return null;
            }
            var r = Create( monitor, required, types, created, visiting, ordered );
            if( r == null )
            {
                visiting.Pop();
                return null;
            }
            args[i] = r;
        }
        visiting.Pop();
        c = (IAppBuilderContributor)ctors[0].Invoke( args );
        created.Add( t, c );
        ordered.Add( c );
        return c;
    }
}
