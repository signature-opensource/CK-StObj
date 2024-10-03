using CK.CodeGen;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace CK.Setup;

/// <summary>
/// Captures properties of a <see cref="IPocoPropertyInfo"/>.
/// <para>
/// This is defined by at least one <see cref="DeclaredProperties"/>. When more than one exists, they must
/// be compatible across the different interfaces.
/// </para>
/// </summary>
public interface IPocoPropertyInfo
{
    /// <summary>
    /// Gets the index of this property in the <see cref="IPocoFamilyInfo.PropertyList"/>.
    /// Indexes starts at 0 and are compact: this can be used to handle optimized serialization
    /// by index (MessagePack) rather than by name (Json).
    /// <para>
    /// Note that the generated backing field is named <c>_v{Index}</c>.
    /// </para>
    /// </summary>
    int Index { get; }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets all the property implementation across the different interfaces.
    /// </summary>
    IReadOnlyList<IExtPropertyInfo> DeclaredProperties { get; }

    /// <summary>
    /// Gets the <see cref="IUnionTypeCollector"/> if this property is a Union type.
    /// </summary>
    IUnionTypeCollector? UnionTypeDefinition { get; }

}
