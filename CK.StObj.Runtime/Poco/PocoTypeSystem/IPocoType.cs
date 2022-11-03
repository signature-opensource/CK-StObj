using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Poco type information.
    /// </summary>
    public interface IPocoType : IAnnotationSet
    {
        /// <summary>
        /// Compact index that uniquely identifies this type
        /// in the <see cref="PocoTypeSystem.AllTypes"/> list.
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the Type. When this is a value type and <see cref="IsNullable"/> is true,
        /// this is a <see cref="Nullable{T}"/>.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets this type's kind.
        /// </summary>
        PocoTypeKind Kind { get; }

        /// <summary>
        /// Gets whether this type is disallowed as a field in a <see cref="ICompositePocoType"/>,
        /// or always allowed, or allowed but requires the <see cref="DefaultValueInfo.DefaultValue"/> to be set.
        /// <para>
        /// Note that a <see cref="DefaultValueInfo.Disallowed"/> type may perfectly be used in a composite type
        /// if and only if a default value specified at the field level can be resolved.
        /// </para>
        /// </summary>
        DefaultValueInfo DefaultValueInfo { get; }

        /// <summary>
        /// Gets whether this type is nullable.
        /// </summary>
        bool IsNullable { get; }

        /// <summary>
        /// Gets the C# name with namespaces and nullabilities of this type.
        /// </summary>
        string CSharpName { get; }

        /// <summary>
        /// Gets the nullable associated type (this if <see cref="IsNullable"/> is true).
        /// </summary>
        IPocoType Nullable { get; }

        /// <summary>
        /// Gets the non nullable associated type (this if <see cref="IsNullable"/> is false).
        /// </summary>
        IPocoType NonNullable { get; }

        /// <summary>
        /// Gets whether the given type is contravariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is contravariant, false otherwise.</returns>
        bool IsWritableType( Type type );

        /// <summary>
        /// Gets whether the given type is covariant with this one.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is covariant, false otherwise.</returns>
        bool IsReadableType( Type type );
    }
}
