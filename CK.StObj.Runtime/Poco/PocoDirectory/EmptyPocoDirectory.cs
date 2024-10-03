using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace CK.Setup;

/// <summary>
/// Null object pattern implementation: the singleton <see cref="Default"/> can be
/// used instead of null reference.
/// </summary>
public class EmptyPocoDirectory : IPocoDirectory
{
    /// <summary>
    /// Gets an empty <see cref="IPocoDirectory"/>.
    /// </summary>
    public static IPocoDirectory Default { get; } = new EmptyPocoDirectory();

    EmptyPocoDirectory() {}

    IReadOnlyList<IPocoFamilyInfo> IPocoDirectory.Families => Array.Empty<IPocoFamilyInfo>();

    IReadOnlyDictionary<Type, IPocoInterfaceInfo> IPocoDirectory.AllInterfaces => ImmutableDictionary<Type, IPocoInterfaceInfo>.Empty;

    IReadOnlyDictionary<string, IPocoFamilyInfo> IPocoDirectory.NamedFamilies => ImmutableDictionary<string, IPocoFamilyInfo>.Empty;

    IReadOnlyDictionary<Type, IReadOnlyList<IPocoFamilyInfo>> IPocoDirectory.OtherInterfaces => ImmutableDictionary<Type, IReadOnlyList<IPocoFamilyInfo>>.Empty;

    IPocoInterfaceInfo? IPocoDirectory.Find( Type pocoInterface ) => null;

}
