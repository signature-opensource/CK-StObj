using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Union type of a given <typeparamref name="T"/> Poco type.
    /// This applies to <see cref="IAbstractPocoType"/> and <see cref="IUnionPocoType"/>.
    /// </summary>
    public interface IAnyOfPocoType<out T> : IPocoType where T : IPocoType
    {
        /// <summary>
        /// Gets the set of allowed types.
        /// </summary>
        IEnumerable<T> AllowedTypes { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IAnyOfPocoType<T> Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IAnyOfPocoType<T> NonNullable { get; }
    }

}
