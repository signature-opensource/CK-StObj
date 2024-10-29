using CK.Core;
using CK.Setup;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;

namespace CK.Engine.TypeCollector;

/// <summary>
/// Group of similar <see cref="BinPathConfiguration"/> in terms of types to consider.
/// </summary>
public sealed partial class BinPathTypeGroup
{
    readonly ImmutableArray<BinPathConfiguration> _configurations;
    readonly string _groupName;
    readonly IReadOnlySet<ICachedType> _types;
    // Regular groups have a BinPathGroup.
    readonly AssemblyCache.BinPathGroup? _assemblyGroup;
    // Unified has the AssemblyCache.
    readonly AssemblyCache? _assemblyCache;
    SHA1Value _signature;

    // Regular group.
    BinPathTypeGroup( ImmutableArray<BinPathConfiguration> configurations,
                      string groupName,
                      AssemblyCache.BinPathGroup assemblyGroup,
                      HashSet<ICachedType> types,
                      SHA1Value signature )
    {
        _configurations = configurations;
        _groupName = groupName;
        _assemblyGroup = assemblyGroup;
        _types = types;
        _signature = signature;
    }

    // Unified group (Signature is Zero).
    BinPathTypeGroup( AssemblyCache assemblyCache,
                      HashSet<ICachedType> allTypes )
    {
        _configurations = ImmutableArray<BinPathConfiguration>.Empty;
        _groupName = "(Unified)";
        _assemblyCache = assemblyCache;
        _types = allTypes;
    }

    /// <summary>
    /// Gets whether no error occured for this group.
    /// </summary>
    public bool Success => _assemblyGroup?.Success ?? true;

    /// <summary>
    /// Gets the name of this group: the comma separated <see cref="BinPathConfiguration.Name"/> for regular groups
    /// and "(Unified)" when <see cref="IsUnifiedPure"/> is true.
    /// </summary>
    public string GroupName => _groupName;

    /// <summary>
    /// Gets the configurations for this group.
    /// This is empty when <see cref="IsUnifiedPure"/> is true.
    /// <para>
    /// All these configurations result in the same set of assemblies and types.
    /// </para>
    /// </summary>
    public ImmutableArray<BinPathConfiguration> Configurations => _configurations;

    /// <summary>
    /// Gets the types to consider in the group.
    /// </summary>
    public IReadOnlySet<ICachedType> Types => _types;

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
    /// <para>
    /// Other <see cref="BinPathTypeGroup"/> than this one may share the same AssemblyGroup if their
    /// ExcludedTypes and Types configurations differ.
    /// </para>
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
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="configuration">The normalized configuration to handle.</param>
    /// <returns>The result (<see cref="Result.Success"/> can be false).</returns>
    public static Result Run( IActivityMonitor monitor, EngineConfiguration configuration )
    {
        using var _ = monitor.OpenInfo( $"Analyzing assemblies and configured types in {configuration.BinPaths.Count} BinPath configurations." );
        // EngineConfiguration.ExternalTypes are handled at the AssemblyCache level (when building the GlobalCacheType).
        var assemblyResult = AssemblyCache.Run( monitor, configuration );

        // Always produce the groups even if assemblyResult.Success is false.
        bool success = assemblyResult.Success;
        var typeCache = assemblyResult.TypeCache;

        // Ok...
        // 1 - Groups the BinPathConfigurations in all Assembly groups by the key based on ExcludedTypes and Types configurations.
        // 2 - Projects the groups by Assembly group and the BinPathConfigurations that share the same key.
        // 3 - Union them (SelectMany).
        // 4 - Computes the BinPathType.GroupName.
        // 5 - Orders these raw groups by their GroupName to be determinitic: the global Result signature will be computed
        //     based on the group signatures (with the most covering one - or the unified - at the first place in the list).
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
                                                    .OrderBy( rawGroup => rawGroup.GroupName )
                                                    .ToArray();
        // Awful optimization here but this avoids a clone of the set of types in 99.9% of cases
        // (mono bin path or multi bin paths with different assemblies).
        foreach( var (_, assemblyGroup, _) in rawGroups ) assemblyGroup._internalTypesRefCount++;

        // Use a List because the groups may contain a new unified one if no covering group exists.
        var groups = new List<BinPathTypeGroup>();

        // Reusable hasher.
        using var hasher = IncrementalHash.CreateHash( HashAlgorithmName.SHA1 );

        foreach( var (configurations, assemblyGroup, groupName) in rawGroups )
        {
            // Instead of computing the signature on the final set of types that would require to sort it by type name,
            // we based the start of our signature on the Excluded and Types configurations and rely on the Assembly.BinPathGroup
            // signature for the rest.
            hasher.AppendData( assemblyGroup.Signature.GetBytes().Span );

            // On error (InternalTypes is null), provide an empty set to the Result.
            HashSet<ICachedType> types;
            if( assemblyGroup.InternalTypes == null )
            {
                Throw.DebugAssert( success is false );
                types = new HashSet<ICachedType>();
            }
            else
            {
                types = assemblyGroup._internalTypesRefCount == 1
                            ? assemblyGroup.InternalTypes
                            : new HashSet<ICachedType>( assemblyGroup.InternalTypes );

                var c = configurations.First();
                // Applying Types and ExcludedTypes configurations to the types provided by the assemblies.
                Throw.DebugAssert( "Configuration normalization did the job.", c.Types.Any( c.ExcludedTypes.Contains ) is false );
                // We must unfortunately order the sets for the signature updates.
                var excludedByConfiguration = c.ExcludedTypes.Select( assemblyResult.TypeCache.Find )
                                                             .Where( cT => cT != null )
                                                             .OrderBy( cT => cT!.CSharpName );
                foreach( var cT in excludedByConfiguration )
                {
                    Throw.DebugAssert( cT != null );
                    var error = cT.Kind.GetUnhandledMessage();
                    if( error != null )
                    {
                        monitor.Warn( $"Ignoring ExcludedType configuration: '{cT.CSharpName}' {error}." );
                    }
                    else
                    {
                        if( types.Remove( cT ) )
                        {
                            // For the hash, consider the type if it has changed the set.
                            hasher.Append( cT.CSharpName );
                        }
                    }
                }

                var includedByConfiguration = c.Types.Select( assemblyResult.TypeCache.Get )
                                                     .OrderBy( cT => cT.CSharpName );
                foreach( var cT in includedByConfiguration )
                {
                    var error = cT.Kind.GetUnhandledMessage();
                    if( error != null )
                    {
                        monitor.Error( $"Invalid Type in configuration for: '{cT.CSharpName}' {error}." );
                        success = false;
                    }
                    else if( (cT.Kind & TypeKind.IsIntrinsicExcluded) != 0 )
                    {
                        monitor.Warn( $"Configured Type '{cT.CSharpName}' is [ExcludeCKType]. Ignoring it." );
                    }
                    else if( types.Add( cT ) )
                    {
                        hasher.Append( cT.CSharpName );
                    }
                }

                // We don't expand the intrinsic [RegisterCKType(...)] types now:
                // this is up to the actual type registration to handle this: this enables new types
                // to be registered if needed by AspectEngine.RunPreCode and allows the final type
                // objects to be registered in one pass.
                Throw.DebugAssert( "Types from assemblies and from configuration have been already filtered.", !types.Any( t => (t.Kind & TypeKind.IsIntrinsicExcluded) != 0 ) );
            }

            var g = new BinPathTypeGroup( configurations,
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
            // ...but why not waiting the unification and accounting the existence of the UnifiedPure
            // or the reordering of the groups (with the most covering one at the start)?
            foreach( var group in groups ) hasher.AppendData( group.Signature.GetBytes().Span );
        }
        return new Result( assemblyResult, groups, new SHA1Value( hasher, resetHasher: false ), success );
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
            var uTypes = new HashSet<ICachedType>();
            // We have no choice here. We must compute the set of IRealObject and IPoco types only for each group
            // to be able to find the covering one (if it exists) and we cannot know if the covering one is needed
            // until we check that a unified group is required.
            var uSets = result.Select( g => new HashSet<ICachedType>( g.Types
                                                                        .Where( t => t is IPocoCachedType or IRealObjectCachedType ) ) )
                              .ToArray();
            // The covering one is necessarily the biggest one if a unification is not required.
            // And an unification is required when the biggest set is still smaller than the union
            // of all the types.
            int maxCount = 0;
            int maxCountIndex = 0;
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
