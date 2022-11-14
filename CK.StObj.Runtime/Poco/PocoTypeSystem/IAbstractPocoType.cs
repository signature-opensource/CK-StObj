using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// An abstract <see cref="IPoco"/>. See <see cref="IPocoDirectory.OtherInterfaces"/>.
    /// </summary>
    public interface IAbstractPocoType : IOneOfPocoType<IPocoType>
    {
        /// <summary>
        /// Gets the set of other abstract IPoco types that are
        /// compatible with this one.
        /// </summary>
        IEnumerable<IAbstractPocoType> OtherAbstractTypes { get; }

        /// <summary>
        /// Gets the set of <see cref="IConcretePocoType"/> of
        /// primary interfaces that are compatible with this abstract type.
        /// <para>
        /// Use <see cref="IUnionPocoType.AllowedTypes"/> for the full set of compatible types.
        /// </para>
        /// </summary>
        IEnumerable<IPrimaryPocoType> PrimaryPocoTypes { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IAbstractPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IAbstractPocoType NonNullable { get; }

    }
}
