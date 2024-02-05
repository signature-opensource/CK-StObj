using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Named types are <see cref="IPrimaryPocoType"/>, <see cref="IRecordPocoType"/> and <see cref="IEnumPocoType"/>.
    /// They may have a <see cref="ExternalNameAttribute"/>.
    /// </summary>
    public interface INamedPocoType : IPocoType
    {
        /// <summary>
        /// Gets the optional <see cref="ExternalNameAttribute"/>.
        /// </summary>
        ExternalNameAttribute? ExternalName { get; }

        /// <summary>
        /// Gets the <see cref="ExternalNameAttribute.Name"/>? or <see cref="IPocoType.CSharpName"/>?.
        /// </summary>
        string ExternalOrCSharpName { get; }

        /// <inheritdoc cref="IPocoType.Nullable" />
        new INamedPocoType Nullable { get; }

        /// <inheritdoc cref="IPocoType.NonNullable" />
        new INamedPocoType NonNullable { get; }

    }
}
