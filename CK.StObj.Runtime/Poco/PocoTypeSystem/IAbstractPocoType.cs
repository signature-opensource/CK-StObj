using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Setup
{
    /// <summary>
    /// An abstract <see cref="IPoco"/>. See <see cref="IPocoDirectory.OtherInterfaces"/>.
    /// </summary>
    public interface IAbstractPocoType : IOneOfPocoType
    {
        /// <summary>
        /// Gets the set of other abstract IPoco types that specialize this one.
        /// <para>
        /// These are nullable types if this type is nullable.
        /// </para>
        /// </summary>
        IEnumerable<IAbstractPocoType> Specializations { get; }

        /// <summary>
        /// Gets the set of abstract IPoco types that generalize this one.
        /// <para>
        /// These are nullable types if this type is nullable.
        /// </para>
        /// <para>
        /// This never contains the root <see cref="CK.Core.IPoco"/> but can contain
        /// the <see cref="CK.Core.IClosedPoco"/>.
        /// </para>
        /// </summary>
        IEnumerable<IAbstractPocoType> Generalizations { get; }

        /// <summary>
        /// Gets the set of <see cref="IPrimaryPocoType"/> of
        /// primary interfaces that are compatible with this abstract type.
        /// <para>
        /// These are nullable types if this type is nullable.
        /// </para>
        /// <para>
        /// Use <see cref="IOneOfPocoType.AllowedTypes"/> for the full set of compatible types
        /// (primary and abstract Poco types).
        /// </para>
        /// </summary>
        IEnumerable<IPrimaryPocoType> PrimaryPocoTypes { get; }

        /// <summary>
        /// Gets the fields.
        /// </summary>
        ImmutableArray<IAbstractPocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IAbstractPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IAbstractPocoType NonNullable { get; }

    }
}
