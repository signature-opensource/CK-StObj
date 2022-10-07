using CK.CodeGen;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extended <see cref="PropertyInfo"/>.
    /// </summary>
    public interface IPocoPropertyImpl
    {
        /// <summary>
        /// Gets the poco property.
        /// </summary>
        IPocoPropertyInfo PocoProperty { get; }

        /// <summary>
        /// Gets the property info.
        /// </summary>
        PropertyInfo Info { get; }

        /// <summary>
        /// Gets whether this property is not writable.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the Poco property type of this property.
        /// </summary>
        PocoPropertyKind PocoPropertyKind { get; }

        /// <summary>
        /// Gets the set of types that define the union type in their non-nullable form.
        /// Empty when not applicable. When applicable the actual type of the property is necessarily compatible
        /// (assignable to) any of the variants. Nullability applies to the whole property.
        /// <para>
        /// The set of possible types is "cleaned up":
        /// <list type="bullet">
        ///     <item>Only one instance of duplicated types is kept.</item>
        ///     <item>When a type and its specializations appear (IsAssignableFrom), only the most general one is kept.</item>
        /// </list>
        /// These rules guaranty that there is no duplicated actual <see cref="NullableTypeTree.Type"/> in any Union.
        /// </para>
        /// </summary>
        IEnumerable<NullableTypeTree> UnionTypes { get; }

        /// <summary>
        /// Gets the nullable type tree type of this property.
        /// </summary>
        NullableTypeTree NullableTypeTree { get; }

    }
}
