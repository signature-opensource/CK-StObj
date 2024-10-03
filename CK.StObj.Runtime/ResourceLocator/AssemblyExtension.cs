using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection;

namespace CK.Core;

/// <summary>
/// Defines extension methods on <see cref="Assembly"/> type.
/// </summary>
public static class AssemblyExtension
{
    static readonly ConcurrentDictionary<Assembly, IReadOnlyList<string>> _cache = new ConcurrentDictionary<Assembly, IReadOnlyList<string>>();

    /// <summary>
    /// Gets all resource names contained in the assembly (calls <see cref="Assembly.GetManifestResourceNames"/>)
    /// as a sorted ascending (thanks to <see cref="StringComparer.Ordinal"/>) cached list of strings.
    /// </summary>
    /// <param name="this">Assembly </param>
    /// <returns>An ordered list of the resource names.</returns>
    static public IReadOnlyList<string> GetSortedResourceNames( this Assembly @this )
    {
        if( @this == null ) throw new ArgumentNullException( "assembly" );
        // We don't care about duplicate computation and set. "Out of lock" Add in GetOrAdd is okay.
        return _cache.GetOrAdd( @this, a =>
        {
            var l = a.GetManifestResourceNames();
            Array.Sort( l, StringComparer.Ordinal );
            return l;
        } );

    }
}
