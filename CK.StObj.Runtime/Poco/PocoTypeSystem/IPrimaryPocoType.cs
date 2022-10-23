using System.Collections.Generic;

namespace CK.Setup
{

    /// <summary>
    /// Type for the primary interface of a <see cref="PocoTypeKind.IPoco"/> family.
    /// </summary>
    public interface IPrimaryPocoType : IConcretePocoType, ICompositePocoType
    {
        /// <inheritdoc cref="ICompositePocoType.Fields"/>
        new IReadOnlyList<IPrimaryPocoField> Fields { get; }

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
