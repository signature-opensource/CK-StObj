using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines a type that must be generated in order to support
    /// the actual types.
    /// <para>
    /// These types are generated in <see cref="Namespace"/>.
    /// </para>
    /// </summary>
    public interface IPocoRequiredSupportType
    {
        /// <summary>
        /// Namespace for the generated support types.
        /// </summary>
        public const string Namespace = "CK.GRSupport";

        /// <summary>
        /// Gets the full type name.
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets the type name without its namespace.
        /// </summary>
        string TypeName { get; }

    }
}
