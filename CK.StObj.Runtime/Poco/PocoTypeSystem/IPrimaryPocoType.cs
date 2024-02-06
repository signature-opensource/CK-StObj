using System.Collections.Generic;

namespace CK.Setup
{

    /// <summary>
    /// Type for the primary interface of a <see cref="PocoTypeKind.PrimaryPoco"/> family.
    /// <para>
    /// This type is its own <see cref="IPocoType.ObliviousType"/>.
    /// </para>
    /// </summary>
    public interface IPrimaryPocoType : ICompositePocoType
    {
        /// <summary>
        /// Gets the poco family information from the <see cref="IPocoDirectory"/>.
        /// </summary>
        IPocoFamilyInfo FamilyInfo { get; }

        /// <inheritdoc cref="ICompositePocoType.Fields"/>
        new IReadOnlyList<IPrimaryPocoField> Fields { get; }

        /// <summary>
        /// Gets the <see cref="IAbstractPocoType"/> that this Poco supports excluding the <see cref="CK.Core.IPoco"/>.
        /// This is the projection of the <see cref="IPocoFamilyInfo.OtherInterfaces"/>.
        /// <para>
        /// The returned types are nullable if this one is the <see cref="Nullable"/>.
        /// </para>
        /// </summary>
        IReadOnlyList<IAbstractPocoType> AbstractTypes { get; }

        /// <summary>
        /// Gets the minimal set of <see cref="AbstractTypes"/>, considering inheritance,
        /// generic parameter variance based on <see cref="IPocoType.CanReadFrom(IPocoType)"/> and
        /// <see cref="IPocoType.CanWriteTo(IPocoType)"/>.
        /// <para>
        /// The returned types are nullable if this one is the <see cref="Nullable"/>.
        /// </para>
        /// </summary>
        IEnumerable<IAbstractPocoType> MinimalAbstractTypes { get; }

        /// <summary>
        /// Gets the secondary interfaces with the same nullability as this one.
        /// Corresponds to the <see cref="IPocoFamilyInfo.Interfaces"/> (except the first one that is this primary Poco type).
        /// </summary>
        IEnumerable<ISecondaryPocoType> SecondaryTypes { get; }

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
