using CK.CodeGen;
using CK.TypeSet;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace CK.Setup;

/// <summary>
/// Root generation context that contains all the applications to generate.
/// </summary>
public sealed class GenContext
{
    readonly ImmutableArray<AppGenContext> _apps;

    internal GenContext( IReadOnlyList<BinPathTypeGroup> typeGroups )
    {
        var apps = new AppGenContext[typeGroups.Count];
        for( int i = 0; i < apps.Length; ++i )
        {
            apps[i] = new AppGenContext( apps.Length, typeGroups[i], CodeWorkspace.Create(), i == 0 ? null : apps[0].Memory );
        }

        _apps = ImmutableCollectionsMarshal.AsImmutableArray( apps );
    }

    /// <summary>
    /// Gets the Application contexts to generate.
    /// </summary>
    public ImmutableArray<AppGenContext> Apps => _apps;
}
