using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Union type of a given <typeparamref name="T"/> Poco type.
    /// This applies to <see cref="IAbstractPocoType"/> and <see cref="IUnionPocoType"/>.
    /// </summary>
    public interface IOneOfPocoType : IPocoType
    {
        /// <summary>
        /// Gets the set of allowed types.
        /// </summary>
        IEnumerable<IPocoType> AllowedTypes { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IOneOfPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IOneOfPocoType NonNullable { get; }
    }

}
