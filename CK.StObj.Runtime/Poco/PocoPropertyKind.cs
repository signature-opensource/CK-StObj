using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Extends the <see cref="PocoTypeKind"/> with <see cref="Union"/>.
    /// </summary>
    public enum PocoPropertyKind
    {
        /// <inheritdoc cref="PocoTypeKind.None"/>
        None = PocoTypeKind.None,

        /// <inheritdoc cref="PocoTypeKind.IPoco"/>
        IPoco = PocoTypeKind.IPoco,

        /// <inheritdoc cref="PocoTypeKind.StandardCollection"/>
        StandardCollection = PocoTypeKind.StandardCollection,

        /// <inheritdoc cref="PocoTypeKind.Basic"/>
        Basic = PocoTypeKind.Basic,

        /// <inheritdoc cref="PocoTypeKind.Basic"/>
        ValueTuple = PocoTypeKind.ValueTuple,

        /// <inheritdoc cref="PocoTypeKind.Enum"/>
        Enum,

        /// <inheritdoc cref="PocoTypeKind.Any"/>
        Any,

        /// <summary>
        /// Union (algebraic type) of one or more (only one is weird!) Poco types.
        /// Such type can be defined on IPoco properties thanks to the <see cref="UnionTypeAttribute"/>.
        /// </summary>
        Union,

    }
}
