using CK.CodeGen;

namespace CK.Setup.PocoJson
{
    /// <summary>
    /// Service engine interface is available when the Json serialization
    /// is supported.
    /// </summary>
    public interface IPocoJsonSerializableServiceEngine
    {
        /// <summary>
        /// Gets the exchangeable Json name map.
        /// <para>
        /// Currently, this is the <see cref="IPocoSerializableServiceEngine.ExchangeableNames"/> but
        /// nothing prevents, in the future, to use different names specifically for Json.
        /// </para>
        /// </summary>
        IPocoTypeNameMap JsonExchangeableNames { get; }

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
