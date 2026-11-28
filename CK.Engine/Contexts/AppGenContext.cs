using CK.CodeGen;
using CK.TypeCache;
using CK.TypeSet;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace CK.Setup;

/// <summary>
/// The App level generation context for a <see cref="BinPathTypeGroup"/>.
/// </summary>
public sealed class AppGenContext
{
    readonly int _appCount;
    readonly BinPathTypeGroup _group;
    readonly ICodeWorkspace _code;
    readonly IReadOnlyDictionary<object, object>? _unifiedMemory;
    readonly Dictionary<object, object> _memory;

    internal AppGenContext( int appContextCount, BinPathTypeGroup group, ICodeWorkspace code, IReadOnlyDictionary<object, object>? unifiedMemory )
    {
        _appCount = appContextCount;
        _group = group;
        _code = code;
        _unifiedMemory = unifiedMemory;
        _memory = new Dictionary<object, object>();
    }

    /// <summary>
    /// Gets the count of <see cref="AppGenContext"/> that must be built.
    /// </summary>
    public int AppCount => _appCount;

    /// <summary>
    /// The application (or group of similar applications) that is being built.
    /// </summary>
    public BinPathTypeGroup Group => _group;

    /// <summary>
    /// The target C# code workspace.
    /// </summary>
    public ICodeWorkspace Code => _code;

    /// <summary>
    /// Gets whether this context is the purely unified one.
    /// <para>
    /// A purely unified context has no IAutoService, only IPoco and IRealObject (it is not
    /// a real application): its <see cref="Code"/> is useless but its <see cref="Memory"/>
    /// becomes the <see cref="UnifiedMemory"/> of the other application contexts.
    /// </para>
    /// </summary>
    public bool IsUnifiedPure => _group.IsUnifiedPure;

    /// <summary>
    /// Gets whether this is the unified context: it is the first one to run and it is guaranteed to contain all
    /// the IPoco and IRealObject of all the other contexts. Its <see cref="UnifiedMemory"/> is null: its own
    /// <see cref="Memory"/> will be the unified's one of the other contexts.
    /// </summary>
    [MemberNotNullWhen( false, nameof( UnifiedMemory ) )]
    public bool IsUnified => _unifiedMemory == null;

    /// <summary>
    /// Gets the unified memory if this context is not the unified one, null otherwise.
    /// </summary>
    public IReadOnlyDictionary<object, object>? UnifiedMemory => _unifiedMemory;

    /// <summary>
    /// Gets this context's memory.
    /// <para>
    /// Using this as a shared context between participants is possible, but the real purpose of this memory is
    /// to become the <see cref="UnifiedMemory"/> of the other application contexts when this <see cref="IsUnified"/> is true.
    /// </para>
    /// </summary>
    public Dictionary<object, object> Memory => _memory;

    /// <summary>
    /// Overridden to return "App '<see cref="BinPathTypeGroup.GroupName"/>'".
    /// </summary>
    public override string ToString() => $"App '{_group.GroupName}'";
}
