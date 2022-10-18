using System.Collections.Generic;

namespace CK.Setup
{

    /// <summary>
    /// Type for <see cref="PocoTypeKind.IPoco"/>.
    /// </summary>
    public interface IConcretePocoType : ICompositePocoType, IUnionPocoType<IConcretePocoType>
    {
        /// <summary>
        /// Gets the poco family.
        /// </summary>
        IPocoFamilyInfo Family { get; }

        /// <summary>
        /// Gets the primary interface.
        /// Can be this poco type.
        /// </summary>
        IConcretePocoType PrimaryInterface { get; }

        /// <inheritdoc cref="ICompositePocoType.Fields" />
        new IReadOnlyList<IConcretePocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IConcretePocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IConcretePocoType NonNullable { get; }

    }
}
