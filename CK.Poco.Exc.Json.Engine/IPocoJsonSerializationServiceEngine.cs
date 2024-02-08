using CK.CodeGen;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// Service engine interface is available when the Json serialization
    /// is supported.
    /// </summary>
    public interface IPocoJsonSerializationServiceEngine
    {
        /// <summary>
        /// Gets the <see cref="IPocoSerializationServiceEngine"/>.
        /// </summary>
        IPocoSerializationServiceEngine SerializableLayer { get; }

        /// <summary>
        /// Gets the Exporter type scope.
        /// </summary>
        ITypeScope Exporter { get; }

        /// <summary>
        /// Gets the Importer type scope.
        /// </summary>
        ITypeScope Importer { get; }
    }
}
