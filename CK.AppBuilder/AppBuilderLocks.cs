using CK.Core;
using CK.TypeSet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Applies the restrictions of the configuration (see <see cref="AppBuilderConfiguration.ApplyLocks"/>) once the
/// contributors have contributed: the <c>"$LOCK"</c> of the "ckonf.json" and the <see cref="ConfigurationLockAttribute"/>
/// of the configurable types of each App.
/// </summary>
static class AppBuilderLocks
{
    public static bool Run( IActivityMonitor monitor, AppBuilderConfiguration configuration, BinPathTypeGroup.Result sets )
    {
        using var _ = monitor.OpenInfo( "Applying the configuration restrictions." );
        var byApp = new Dictionary<AppConfiguration, List<PackageConfigurationLock>>();
        foreach( var g in sets.Groups )
        {
            if( g.IsUnifiedPure ) continue;
            var apps = configuration.Apps.Where( a => g.Configurations.Contains( a.BinPathConfiguration ) ).ToList();
            foreach( var t in g.ConfigurableTypes )
            {
                var locks = t.Type.GetCustomAttributes<ConfigurationLockAttribute>( inherit: false ).ToList();
                if( locks.Count == 0 || !t.TryGetConfigurableType( monitor, out var attr ) || attr == null ) continue;
                foreach( var a in apps )
                {
                    if( !byApp.TryGetValue( a, out var list ) ) byApp.Add( a, list = new List<PackageConfigurationLock>() );
                    list.AddRange( locks.Select( l => new PackageConfigurationLock( t.CSharpName, attr.Name, l ) ) );
                }
            }
        }
        return configuration.ApplyLocks( monitor, a => byApp.GetValueOrDefault( a ) ?? [] );
    }
}
