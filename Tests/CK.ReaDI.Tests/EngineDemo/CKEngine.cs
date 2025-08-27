using CK.Core;
using CK.Engine.TypeCollector;
using CK.Setup;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Demo;

public static class CKEngine
{
    public static async Task<bool> RunAsync( this EngineConfiguration configuration, IActivityMonitor monitor )
    {
        if( !configuration.NormalizeConfiguration( monitor ) )
        {
            return false;
        }
        var typeGroups = BinPathTypeGroup.Run( monitor, configuration );
        if( typeGroups.Success )
        {
            var e = new ReaDIEngine( typeGroups.TypeCache, debugMode: true );
            if( CreateAndAddAspects( monitor, e, configuration )
                && e.AddObject( monitor, configuration ) )
            {
                foreach( var g in typeGroups.Groups )
                {
                    g.ConfiguredTypes.
                }
                if( ! )
                {
                    return false;
                }
            }
            return false;
        }
    }

    static bool CreateAndAddAspects( IActivityMonitor monitor, ReaDIEngine engine, EngineConfiguration configuration )
    {
        bool success = true;
        using( monitor.OpenTrace( $"Creating and configuring {configuration.Aspects.Count} aspect(s)." ) )
        {
            var aspectsType = new HashSet<Type>();
            var configArgs = new object[1];
            foreach( var c in configuration.Aspects )
            {
                Type? t = SimpleTypeFinder.WeakResolver( c.AspectType, true );
                Throw.DebugAssert( t != null );
                if( !aspectsType.Add( t ) )
                {
                    monitor.Error( $"Aspect '{t:N}' occurs more than once in configuration." );
                    success = false;
                }
                else
                {
                    configArgs[0] = c;
                    var oAspect = Activator.CreateInstance( t, configArgs );
                    if( oAspect is not EngineAspect aspect )
                    {
                        monitor.Error( $"Aspect '{t:N}' is not a EngineAspect." );
                        success = false;
                    }
                    else
                    {
                        success &= engine.AddObject( monitor, aspect );
                    }
                }
            }
        }
        return success;
    }
}


public sealed class AlsoRegisterTypeHandler : IReaDIHandler
{

    [ReaDI]
    public PreRunningGroup? AlsoRegister( IActivityMonitor monitor, [ReaDILoop]BinPathTypeGroup g )
    {

    }
}

[HierarchicalTypeRoot]
public sealed class PreRunningGroup
{
    readonly ReaDIEngine _engine;
    readonly BinPathTypeGroup _group;
    readonly HashSet<ICachedType> _types;

    internal PreRunningGroup( ReaDIEngine engine, BinPathTypeGroup group )
    {
        _engine = engine;
        _group = group;
        _types = new HashSet<ICachedType>( group.ConfiguredTypes.AllTypes );
    }

    /// <summary>
    /// Gets the lower-level group that has been discovered.
    /// </summary>
    public BinPathTypeGroup BinPathTypeGroup => _group;

    /// <summary>
    /// Gets the name of this group: the comma separated <see cref="BinPathConfiguration.Name"/> for regular groups
    /// and "(Unified)" when <see cref="IsUnifiedPure"/> is true.
    /// </summary>
    public string GroupName => _group.GroupName;

    /// <summary>
    /// Gets whether this group is the purely unified one.
    /// When true, no similar configuration exist (<see cref="Configurations"/> is empty).
    /// <para>
    /// This unified BinPath has no IAutoService, only IPoco and IRealObject, but all the IPoco and all the IRealObject
    /// of all the other BinPaths.
    /// This BinPath is only used to enables Aspects that interact with the real world to handle every aspect (!) of the IRealObject
    /// during the code generation but this code will never be used.
    /// </para>
    /// </summary>
    public bool IsUnifiedPure => _group.IsUnifiedPure;

    /// <summary>
    /// Gets the configurations for this group.
    /// This is empty when <see cref="IsUnifiedPure"/> is true.
    /// <para>
    /// All these configurations result in the same set of assemblies and types.
    /// </para>
    /// </summary>
    public ImmutableArray<BinPathConfiguration> Configurations => _group.Configurations;

    /// <summary>
    /// Gets all the types that must be registered, including the ones in <see cref="ConfiguredTypes"/>.
    /// </summary>
    public IReadOnlySet<ICachedType> AllTypes => _types;

    /// <summary>
    /// Gets the type configurations with a <see cref="TypeConfiguration.Kind"/> that is not <see cref="ConfigurableAutoServiceKind.None"/>.
    /// <para>
    /// TypeConfiguration uses <see cref="Type"/>, not <see cref="ICachedType"/> because it comes from the configuration.
    /// </para>
    /// </summary>
    IReadOnlyCollection<TypeConfiguration> ConfiguredTypes => _group.ConfiguredTypes.ConfiguredTypes;

    /// <summary>
    /// Gets this BinPathTypeGroup digital signature. Based on <see cref="AssemblyCache.BinPathGroup.Signature"/>
    /// and the <see cref="BinPathConfiguration.ExcludedTypes"/> and <see cref="BinPathConfiguration.Types"/>
    /// content.
    /// <para>
    /// This is the <see cref="SHA1Value.Zero"/> if <see cref="IsUnifiedPure"/> is true or <see cref="Success"/> is false.
    /// </para>
    /// </summary>
    public SHA1Value Signature => _group.Signature;

    /// <summary>
    /// Enables to add new types to <see cref="AllTypes"/>.
    /// </summary>
    /// <param name="type">Type to consider.</param>
    public bool AlsoRegister( IActivityMonitor monitor, ICachedType type )
    {
        if( _types.Add( type ) )
        {
            return AddEngineAtrributeReaDIHandler( monitor, type, _engine );
        }
        return true;
    }

    private static bool AddEngineAtrributeReaDIHandler( IActivityMonitor monitor, ICachedType type, ReaDIEngine engine )
    {
        if( !type.TryGetAllAttributes( monitor, out var attributes ) )
        {
            return false;
        }
        bool success = true;
        foreach( var a in attributes )
        {
            if( a is IReaDIHandler handler )
            {
                success &= engine.AddObject( monitor, handler );
            }
        }
        return success;
    }
}
