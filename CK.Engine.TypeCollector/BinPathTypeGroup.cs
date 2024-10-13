using CK.Core;
using CK.Setup;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Transactions;

namespace CK.Engine.TypeCollector;

/// <summary>
/// 
/// </summary>
public sealed partial class BinPathTypeGroup
{
    readonly ImmutableArray<BinPathConfiguration> _configurations;
    readonly string _groupName;
    readonly IConfiguredTypeSet _configuredTypes;
    // Regular groups have a BinPathGroup.
    readonly AssemblyCache.BinPathGroup? _assemblyGroup;
    // Unified has the AssemblyCache.
    readonly AssemblyCache? _assemblyCache;
    SHA1Value _signature;

    // Regular group.
    BinPathTypeGroup( ImmutableArray<BinPathConfiguration> configurations,
                      string groupName,
                      AssemblyCache.BinPathGroup assemblyGroup,
                      IConfiguredTypeSet configuredTypes,
                      SHA1Value signature )
    {
        _configurations = configurations;
        _groupName = groupName;
        _assemblyGroup = assemblyGroup;
        _configuredTypes = configuredTypes;
        _signature = signature;
    }

    // Unified group (Signature is Zero).
    BinPathTypeGroup( AssemblyCache assemblyCache,
                      HashSet<Type> allTypes )
    {
        _configurations = ImmutableArray<BinPathConfiguration>.Empty;
        _groupName = "(Unified)";
        _assemblyCache = assemblyCache;
        _configuredTypes = new ImmutableConfiguredTypeSet( allTypes );
    }

    /// <summary>
    /// Gets whether no error occured for this group.
    /// </summary>
    public bool Success => _assemblyGroup?.Success ?? true;

    /// <summary>
    /// Gets the name of this group: the comma separated <see cref="BinPathConfiguration.Name"/> for regular groups
    /// and "_Unified_" when <see cref="IsUnifiedPure"/> is true.
    /// </summary>
    public string GroupName => _groupName;

    /// <summary>
    /// Gets the configurations for this group.
    /// This is empty when <see cref="IsUnifiedPure"/> is true.
    /// </summary>
    public ImmutableArray<BinPathConfiguration> Configurations => _configurations;

    /// <summary>
    /// Gets the types to consider in the group.
    /// </summary>
    public IConfiguredTypeSet ConfiguredTypes => _configuredTypes;

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
    public bool IsUnifiedPure => _assemblyCache != null;

    /// <summary>
    /// Gets the assembly level information for this group or null if this
    /// is the unified one.
    /// </summary>
    public AssemblyCache.BinPathGroup? AssemblyGroup => _assemblyGroup;

    /// <summary>
    /// Gets the assembly cache.
    /// </summary>
    public AssemblyCache AssemblyCache => _assemblyGroup?.AssemblyCache ?? _assemblyCache!;

    /// <summary>
    /// Gets this BinPathTypeGroup digital signature. Based on <see cref="AssemblyCache.BinPathGroup.Signature"/>
    /// and the <see cref="BinPathConfiguration.ExcludedTypes"/> and <see cref="BinPathConfiguration.Types"/>
    /// content.
    /// <para>
    /// This is the <see cref="SHA1Value.Zero"/> if <see cref="IsUnifiedPure"/> is true or <see cref="Success"/> is false.
    /// </para>
    /// </summary>
    public SHA1Value Signature => _signature;

    /// <summary>
    /// Creates one or more <see cref="BinPathTypeGroup"/> from a configuration.
    /// <para>
    /// This step cannot fail: A false <see cref="Result.Success"/> comes from the assembly level.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="configuration">The normalized configuration to handle.</param>
    /// <returns>The result (<see cref="Result.Success"/> can be false).</returns>
    public static Result Run( IActivityMonitor monitor, EngineConfiguration configuration )
    {
        using var _ = monitor.OpenInfo( $"Analyzing assemblies and configured types in {configuration.BinPaths.Count} BinPath configurations." );
        var assemblyResult = TypeCollector.AssemblyCache.Run( monitor, configuration );


        // Always produce the groups even if assemblyResult.Success is false.
        var groups = new List<BinPathTypeGroup>();
        // Ok...
        // 1 - Groups the BinPathConfigurations in all Assembly groups by the key based on ExcludedTypes and Types configurations.
        // 2 - Projects the groups by Assembly group and the BinPathConfigurations that share the same key.
        // 3 - Union them (SelectMany).
        // 4 - Computes the BinPathType.GroupName.
        // 5 - Orders these raw groups by their GroupName to compute the global Result signature.
        var rawGroups = assemblyResult.BinPathGroups.Select( g => g.Configurations.GroupBy( b => new GroupKey( g, b ) ) )
                                                    .SelectMany( gType => gType.Select( tG => (Configurations: tG.OrderBy( b => b.Name ).ToImmutableArray(),
                                                                                              tG.Key.AssemblyGroup) ) )
                                                    .Select( rawGroup => (
                                                                            rawGroup.Configurations,
                                                                            rawGroup.AssemblyGroup,
                                                                            GroupName: rawGroup.Configurations.Length == rawGroup.AssemblyGroup.Configurations.Count
                                                                                        ? rawGroup.AssemblyGroup.GroupName
                                                                                        : rawGroup.Configurations.Select( b => b.Name ).Concatenate( "-" )
                                                                          ) )
                                                    .OrderBy( rawGroup => rawGroup.GroupName );

        // Reusable hasher.
        using var hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );

        foreach( var (configurations, assemblyGroup, groupName) in rawGroups )
        {
            var sourceName = $"BinTypePathGoup '{groupName}'";
            var c = configurations.First();

            // Clones the assembly configured types as there may be other BinPathTypeGroup that use it
            // (don't currently try to opimize here as it would mutate the assemblyGroup.ConfiguredTypes that should not be immutable
            // since it is publicy exposed... or make it internal).
            var types = new ConfiguredTypeSet( assemblyGroup.ConfiguredTypes );

            // Instead of computing the signature on the final set of types that would require to sort it by type name,
            // we based the start of our signature on the Excluded and Types configurations and rely on the Assembly.BinPathGroup
            // signature for the rest.
            hasher.AppendData( assemblyGroup.Signature.GetBytes().Span );
            // Must unfortunately order the sets.
            foreach( var t in c.ExcludedTypes.Select( t => (Type: t, t.FullName) ).OrderBy( t => t.FullName ) )
            {
                types.Remove( t.Type );
                hasher.Append( t.FullName! );
            }
            Throw.DebugAssert( "Normalized did the job.", c.Types.All( tc => TypeConfiguration.GetConfiguredTypeErrorMessage( tc.Type, tc.Kind ) == null ) );
            foreach( var tc in c.Types.Select( tc => (tc.Type, tc.Kind, tc.Type.FullName) ).OrderBy( tc => tc.FullName ) )
            {
                types.Add( monitor, sourceName, tc.Type, tc.Kind );
                hasher.Append( tc.FullName! ).Append( tc.Kind );
            }
            var g = new BinPathTypeGroup( configurations.ToImmutableArray(),
                                          groupName,
                                          assemblyGroup,
                                          types,
                                          new SHA1Value( hasher, resetHasher: true ) );
            groups.Add( g );
        }
        // The groups list is ordered by GroupName. We may compute the signature here... 
        if( assemblyResult.Success )
        {
            HandleUnifiedBinPath( monitor, groups );
            // ...but why not waiting the unification and accounting the existenc of the UnifiedPure
            // or the reordering of the groups (with the most covering one at the start)?
            foreach( var group in groups ) hasher.AppendData( group.Signature.GetBytes().Span );
        }
        return new Result( assemblyResult, groups, new SHA1Value( hasher, resetHasher: false ) );
    }

    static void HandleUnifiedBinPath( IActivityMonitor monitor, List<BinPathTypeGroup> result )
    {
        Throw.DebugAssert( result.Count != 0 );
        if( result.Count == 1 )
        {
            monitor.Info( "There is only one BinPath to process. Unification is not required." );
        }
        else
        {
            var uTypes = new HashSet<Type>();
            // We have no choice here. We must compute the set of IRealObject and IPoco types only for each group
            // to be able to find the covering one (if it exists) and we cannot know if the covering one is needed
            // until we check that a unified group is required.
            var uSets = result.Select( g => new HashSet<Type>( g.ConfiguredTypes.AllTypes
                                                                .Where( t => typeof( IRealObject ).IsAssignableFrom( t ) || typeof( IPoco ).IsAssignableFrom( t ) ) ) )
                              .ToArray();
            // The covering one is necessarily the biggest one if a unification is not required.
            int maxCount = 0;
            int maxCountIndex = -1;
            for( int i = 0; i < uSets.Length; i++ )
            {
                var uS = uSets[i];
                uTypes.AddRange( uS );
                if( maxCount < uS.Count )
                {
                    maxCount = uS.Count;
                    maxCountIndex = i;
                }
            }
            Throw.DebugAssert( uTypes.Count >= maxCount );
            if( maxCount == uTypes.Count )
            {
                var covering = result[maxCountIndex];
                monitor.Info( $"No unification required: BinPathGroup '{covering.GroupName}' covers the whole set of {uTypes.Count} IRealObject and IPoco." );
                if( maxCountIndex > 0 )
                {
                    result.RemoveAt( maxCountIndex );
                    result.Insert( 0, covering );
                }
            }
            else
            {
                monitor.Info( $"Unification is required for {uTypes.Count} IRealObject and IPoco." );
                var unified = new BinPathTypeGroup( result[0].AssemblyCache, uTypes );
                result.Insert( 0, unified );
            }
        }
    }

    public override string ToString() => $"BinTypePathGoup '{_groupName}'";
}
