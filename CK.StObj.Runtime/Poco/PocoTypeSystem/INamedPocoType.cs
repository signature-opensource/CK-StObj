using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Named types are <see cref="ICompositePocoType"/> (<see cref="IPrimaryPocoType"/>
    /// and <see cref="IRecordPocoType"/> for non anonymous records) and <see cref="IEnumPocoType"/>.
    /// </summary>
    public interface INamedPocoType
    {
        /// <summary>
        /// Gets the optional <see cref="ExternalNameAttribute"/>.
        /// </summary>
        ExternalNameAttribute? ExternalName { get; }

        /// <summary>
        /// Gets the <see cref="ExternalNameAttribute.Name"/> or <see cref="IPocoType.CSharpName"/>.
        /// This is a non nullable name (never ends with '?').
        /// </summary>
        string ExternalOrCSharpName { get; }
    }
}
