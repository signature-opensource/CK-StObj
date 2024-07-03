using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Type for <see cref="PocoTypeKind.Enum"/>.
    /// </summary>
    public interface IEnumPocoType : IPocoType, INamedPocoType
    {
        /// <summary>
        /// Gets the underlying enumeration type.
        /// This is the nullable integral type if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IPocoType UnderlyingType { get; }

        /// <summary>
        /// Gets the default value enumeration name in the form "FullName.None".
        /// It corresponds to the smallest unsigned numerical value: it is usually the name with the 0 (default) value
        /// of the underlying type.
        /// </summary>
        string DefaultValueName { get; }

        /// <summary>
        /// Gets the enum name and associated values.
        /// <para>
        /// This can be empty. It is valid i C# (and in TypeScript) to have enum without values: the
        /// single default and accepted value is 0.
        /// This is a pathological case: we accept this but <see cref="IPocoTypeSet"/> excludes it by default.
        /// </para>
        /// </summary>
        IReadOnlyDictionary<string, object> Values { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType"/>
        /// <remarks>
        /// <see cref="IEnumPocoType"/> returns this.
        /// </remarks>
        new IEnumPocoType ObliviousType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IEnumPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IEnumPocoType NonNullable { get; }

    }
}
