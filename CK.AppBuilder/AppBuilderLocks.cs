using CK.Core;
using CK.TypeSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup;

/// <summary>
/// Applies the restrictions of the configuration (see <see cref="AppBuilderConfiguration.ApplyLocks"/>) once the
/// contributors have contributed: the <c>"$LOCK"</c> of the "ckonf.json" and the <see cref="ConfigurationLockAttribute"/>
/// of the configurable types of each App, in the sections of the type (its configurable name, unless a contributor
/// resolves them: <see cref="IAppBuilderContributor.ResolveSections"/>). Then checks the secrets
/// (<see cref="AppBuilderConfiguration.CheckSecrets"/>) with the <see cref="ConfigurationSecretAttribute"/> of the same
/// types, and the values per deployment of the deployments of the solution (<see cref="CentralDeployment.CheckPerDeployment"/>)
/// with their <see cref="ConfigurationPerDeploymentAttribute"/>. The build of a deployment writes the manifests of its
/// hosts instead (<see cref="AppBuilderConfiguration.WriteDeploymentManifests"/>).
/// </summary>
static class AppBuilderLocks
{
    public static bool Run( IActivityMonitor monitor,
                            AppBuilderConfiguration configuration,
                            BinPathTypeGroup.Result sets,
                            IReadOnlyList<IAppBuilderContributor> contributors,
                            CentralConfiguration? central = null,
                            NormalizedPath builderFolder = default,
                            IReadOnlyList<CentralDeployment>? deployments = null,
                            CentralDeployment? deployment = null )
    {
        using var _ = monitor.OpenInfo( "Applying the configuration restrictions." );
        var byApp = new Dictionary<AppConfiguration, List<PackageConfigurationLock>>();
        var secretsByApp = new Dictionary<AppConfiguration, List<PackageConfigurationSecret>>();
        var perDeploymentByApp = new Dictionary<AppConfiguration, List<PackageConfigurationPerDeployment>>();
        foreach( var g in sets.Groups )
        {
            if( g.IsUnifiedPure ) continue;
            var apps = configuration.Apps.Where( a => g.Configurations.Contains( a.BinPathConfiguration ) ).ToList();
            // The sections of the App singletons and their run values ([ConfigurationSection], [ConfigurationLevel( Run )]).
            bool success = true;
            foreach( var t in g.ConfiguredTypes.AllTypes )
            {
                if( ConfigurationRunProperty.GetSection( t.Type ) is not string section ) continue;
                if( !ConfigurationRunProperty.TryCollect( monitor, t.Type, out var runProperties ) )
                {
                    success = false;
                    continue;
                }
                foreach( var a in apps )
                {
                    a.DeclareInitSection( section );
                    foreach( var p in runProperties ) a.DeclareRunValue( p.Path );
                }
            }
            if( !success ) return false;
            IReadOnlyCollection<Type>? appTypes = null;
            foreach( var t in g.ConfigurableTypes )
            {
                var locks = t.Type.GetCustomAttributes<ConfigurationLockAttribute>( inherit: false ).ToList();
                var secrets = t.Type.GetCustomAttributes<ConfigurationSecretAttribute>( inherit: false ).ToList();
                var perDeployment = t.Type.GetCustomAttributes<ConfigurationPerDeploymentAttribute>( inherit: false ).ToList();
                if( (locks.Count == 0 && secrets.Count == 0 && perDeployment.Count == 0) || !t.TryGetConfigurableType( monitor, out var attr ) || attr == null ) continue;
                appTypes ??= g.ConfigurableTypes.Select( c => c.Type ).ToArray();
                var sections = contributors.Select( c => c.ResolveSections( t.Type, appTypes ) ).FirstOrDefault( s => s != null )?.ToArray()
                               ?? [attr.Name];
                if( sections.Length == 0 )
                {
                    monitor.Warn( $"The [ConfigurationLock], [ConfigurationSecret] and [ConfigurationPerDeployment] of '{t.CSharpName}' apply to no section: the type has no owner in the App." );
                    continue;
                }
                foreach( var a in apps )
                {
                    if( !byApp.TryGetValue( a, out var list ) ) byApp.Add( a, list = new List<PackageConfigurationLock>() );
                    list.AddRange( locks.Select( l => new PackageConfigurationLock( t.CSharpName, sections, l ) ) );
                    if( !secretsByApp.TryGetValue( a, out var sList ) ) secretsByApp.Add( a, sList = new List<PackageConfigurationSecret>() );
                    sList.AddRange( secrets.Select( s => new PackageConfigurationSecret( t.CSharpName, sections, s ) ) );
                    if( !perDeploymentByApp.TryGetValue( a, out var pList ) ) perDeploymentByApp.Add( a, pList = new List<PackageConfigurationPerDeployment>() );
                    pList.AddRange( perDeployment.Select( p => new PackageConfigurationPerDeployment( t.CSharpName, sections, p.Path ) ) );
                }
            }
        }
        if( !configuration.ApplyLocks( monitor, a => byApp.GetValueOrDefault( a ) ?? [] )
            || !configuration.CheckSecrets( monitor, a => secretsByApp.GetValueOrDefault( a ) ?? [] ) )
        {
            return false;
        }
        if( deployment == null && (central == null || deployments == null || deployments.Count == 0) ) return true;
        // The values per deployment of the Apps of this builder.
        bool ok = true;
        var appKeys = new Dictionary<string, IReadOnlyList<(string Pattern, string Source)>>( StringComparer.OrdinalIgnoreCase );
        foreach( var a in configuration.Apps )
        {
            ok &= AppBuilderConfiguration.CollectPerDeploymentKeys( monitor, a, perDeploymentByApp.GetValueOrDefault( a ) ?? [], out var keys );
            appKeys[a.Name] = keys;
        }
        if( !ok ) return false;
        // The build of a deployment: the manifests of its hosts.
        if( deployment != null ) return configuration.WriteDeploymentManifests( monitor, deployment, appKeys );
        Throw.DebugAssert( central != null && deployments != null );
        using( monitor.OpenInfo( $"Checking the values per deployment of {deployments.Count} deployment(s)." ) )
        {
            foreach( var d in deployments )
            {
                ok &= d.CheckPerDeployment( monitor, central, builderFolder, appKeys );
            }
        }
        return ok;
    }
}
