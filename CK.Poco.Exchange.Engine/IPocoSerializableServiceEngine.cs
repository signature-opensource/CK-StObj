namespace CK.Setup
{
    /// <summary>
    /// Exposes the serializable <see cref="SerializableNames"/>.
    /// </summary>
    public interface IPocoSerializableServiceEngine
    {
        /// <summary>
        /// Gets the standard names of the serializable Poco types.
        /// <para>
        /// This map is bound (see <see cref="PocoTypeNameMap.TypeSet"/>) to the <see cref="IPocoTypeSetManager.AllSerializable"/>
        /// and uses the standard names described by <see cref="PocoTypeNameMap"/>.
        /// </para>
        /// </summary>
        PocoTypeNameMap SerializableNames { get; }

        /// <summary>
        /// Gets a name map bound to the <see cref="IPocoTypeSetManager.AllExchangeable"/> that is a subset of the serializable
        /// types. Using this map restricts the types to be exchangeable otherwise <see cref="IPocoTypeNameMap.GetName(IPocoType)"/>
        /// throws.
        /// </summary>
        IPocoTypeNameMap ExchangeableNames { get; }
    }
}
