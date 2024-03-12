using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines a type that must be generated in order to support
    /// the actual types or a type that already exists like the ones in <see cref="CovariantHelpers"/>.
    /// <para>
    /// When generated, these types are generated in <see cref="Namespace"/>.
    /// </para>
    /// </summary>
    public interface IPocoRequiredSupportType
    {
        /// <summary>
        /// Namespace for the support types when they are generated.
        /// </summary>
        public const string Namespace = "CK.GRSupport";

        /// <summary>
        /// Gets whether this type must be generated specifically or if it is a
        /// standard adapter like <see cref="CovariantHelpers.CovNotNullValueDictionary{TKey, TValue}"/>.
        /// </summary>
        bool IsGenerated { get; }

        /// <summary>
        /// Gets the full type name.
        /// </summary>
        string FullName { get; }

        /// <summary>
        /// Gets the type name without its namespace.
        /// </summary>
        string TypeName { get; }

        /// <summary>
        /// Gets the Poco type that this generated type supports.
        /// </summary>
        IPocoType SupportedType { get; }
    }
}
