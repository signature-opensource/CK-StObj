using CK.Core;
using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Basic reference types are the wellknown types: string, <see cref="ExtendedCultureInfo"/>, <see cref="NormalizedCultureInfo"/>, <see cref="MCString"/>
    /// and <see cref="CodeString"/>.
    /// </summary>
    public interface IBasicRefPocoType : IPocoType
    {
        /// <summary>
        /// Gets the base type. This is null when this type specializes <see cref="object"/>.
        /// This is nullable if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IBasicRefPocoType? BaseType {  get; }

        /// <summary>
        /// Gets the base types chain up to the one that specializes <c>object</c>.
        /// These are nullables if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IEnumerable<IBasicRefPocoType> BaseTypes { get; }

        /// <summary>
        /// Gets the set of direct specializations of this type.
        /// These are nullables if <see cref="IPocoType.IsNullable"/> is true.
        /// </summary>
        IEnumerable<IBasicRefPocoType> Specializations { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType"/>
        /// <remarks>
        /// <see cref="IBasicRefPocoType"/> returns the <see cref="Nullable"/>.
        /// </remarks>
        new IBasicRefPocoType ObliviousType { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IBasicRefPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IBasicRefPocoType NonNullable { get; }

    }

}
