using System.Collections.Generic;

namespace CK.Setup
{

    /// <summary>
    /// Type for the primary interface of a <see cref="PocoTypeKind.IPoco"/> family.
    /// </summary>
    public interface IPrimaryPocoType : ICompositePocoType
    {
        /// <summary>
        /// Gets the poco family information from the <see cref="IPocoDirectory"/>.
        /// </summary>
        IPocoFamilyInfo FamilyInfo { get; }

        /// <inheritdoc cref="ICompositePocoType.Fields"/>
        new IReadOnlyList<IPrimaryPocoField> Fields { get; }

        /// <inheritdoc cref="IPocoType.ObliviousType"/>
        new IPrimaryPocoType ObliviousType { get; }

        /// <summary>
        /// Gets the <see cref="IAbstractPocoType"/> that this Poco supports,
        /// excluding the <see cref="CK.Core.IPoco"/> and the <see cref="CK.Core.IClosedPoco"/>.
        /// This is the projection of the <see cref="IPocoFamilyInfo.OtherInterfaces"/>.
        /// </summary>
        IReadOnlyList<IAbstractPocoType> AbstractTypes { get; }

        /// <summary>
        /// Gets the constructor source code.
        /// </summary>
        string CSharpBodyConstructorSourceCode { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new IPrimaryPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new IPrimaryPocoType NonNullable { get; }

    }
}
