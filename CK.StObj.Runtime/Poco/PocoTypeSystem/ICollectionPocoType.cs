using System.Collections.Generic;
using static CK.Setup.IPocoType;

namespace CK.Setup
{
    /// <summary>
    /// Collection type is <see cref="PocoTypeKind.Array"/>, <see cref="PocoTypeKind.HashSet"/>,
    /// <see cref="PocoTypeKind.Dictionary"/> or <see cref="PocoTypeKind.List"/>.
    /// </summary>
    public interface ICollectionPocoType : IPocoType
    {
        /// <summary>
        /// Gets the generic parameters or the array element type.
        /// </summary>
        IReadOnlyList<IPocoType> ItemTypes { get; }

        /// <summary>
        /// Gets the associated <see cref="IRegularAndNominalInfo"/>.
        /// <para>
        /// This is null if and only if this is a <see cref="PocoTypeKind.Array"/>.
        /// </para>
        /// </summary>
        IRegularAndNominalInfo? NominalAndRegularInfo { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new ICollectionPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new ICollectionPocoType NonNullable { get; }

        /// <summary>
        /// Captures the <see cref="List{T}"/>, <see cref="HashSet{T}"/> or <see cref="Dictionary{TKey, TValue}"/>
        /// that a (non array) collection implements.
        /// <para>
        /// This provides the concrete base type that may be specialized: operations that reads data (typically for serialization)
        /// can rely on this type and its unique <see cref="Index"/> to factorizes code.
        /// </para>
        /// </summary>
        public interface IRegularAndNominalInfo
        {
            /// <summary>
            /// Gets the type name (in System.Collections.Generic namespace).
            /// </summary>
            string TypeName { get; }

            /// <summary>
            /// Gets the item types of the collection.
            /// </summary>
            IReadOnlyList<IPocoType> ItemTypes { get; }

            /// <summary>
            /// Gets a unique index that identifies this base type.
            /// </summary>
            int Index { get; }
        }

    }
}
