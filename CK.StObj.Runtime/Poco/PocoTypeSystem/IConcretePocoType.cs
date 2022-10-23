using System.Collections.Generic;

namespace CK.Setup
{

    /// <summary>
    /// Type for <see cref="PocoTypeKind.IPoco"/>.
    /// </summary>
    public interface IConcretePocoType : IAnyOfPocoType<IConcretePocoType>
    {
        /// <summary>
        /// Gets the poco family information from the <see cref="IPocoDirectory"/>.
        /// </summary>
        IPocoFamilyInfo FamilyInfo { get; }

        /// <summary>
        /// Gets the primary interface.
        /// Can be this poco type.
        /// </summary>
        IPrimaryPocoType PrimaryInterface { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IConcretePocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IConcretePocoType NonNullable { get; }

    }
}
