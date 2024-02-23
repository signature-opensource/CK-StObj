using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Exposes the serializable <see cref="SerializableNames"/> and other data
    /// useful to work on serialization and exchangeablity.
    /// </summary>
    public interface IPocoSerializationServiceEngine
    {
        /// <summary>
        /// Gets the <see cref="IPocoTypeSystem"/>.
        /// </summary>
        IPocoTypeSystem TypeSystem { get; }

        /// <summary>
        /// Gets the names of the serializable Poco types.
        /// <para>
        /// This map is bound (see <see cref="IPocoTypeNameMap.TypeSet"/>) to the <see cref="IPocoTypeSetManager.AllSerializable"/>
        /// and uses the standard names described by <see cref="PocoTypeNameMap"/>.
        /// </para>
        /// </summary>
        IPocoTypeNameMap SerializableNames { get; }

        /// <summary>
        /// Gets the <see cref="IPocoTypeSetManager.AllSerializable"/> set.
        /// </summary>
        IPocoTypeSet AllSerializable { get; }

        /// <summary>
        /// Gets the <see cref="IPocoTypeSetManager.AllExchangeable"/> set.
        /// </summary>
        IPocoTypeSet AllExchangeable { get; }

        /// <summary>
        /// Gets the name of the "GetFilter" static function to call with a string to obtain
        /// the named <see cref="ExchangeableRuntimeFilter"/>.
        /// </summary>
        string GetExchangeableRuntimeFilterStaticFunctionName { get; }

        /// <summary>
        /// Gets a unique compact index for a type (that must be serializable).
        /// A nullable type has the negative value of its non nullable counterpart.
        /// </summary>
        /// <param name="t">The nullable or non nullable type.</param>
        /// <returns>A unique index in the <see cref="AllSerializable"/> set.</returns>
        int GetSerializableIndex( IPocoType t );

        /// <summary>
        /// Registers a new <see cref="ExchangeableRuntimeFilter"/> that will be available
        /// in <see cref="PocoExchangeService.RuntimeFilters"/> service.
        /// <para>
        /// Names must be unique and the <paramref name="typeSet"/> must be a subset of the
        /// <see cref="IPocoTypeSetManager.AllSerializable"/> set.
        /// </para>
        /// <para>
        /// The same named set can be registered multiple times as long as its content is the same
        /// as the first one.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="name">The runtime filter name.</param>
        /// <param name="typeSet">The type set.</param>
        /// <returns>Ture on success, false otherwise.</returns>
        bool RegisterExchangeableRuntimeFilter( IActivityMonitor monitor, string name, IPocoTypeSet typeSet );

    }
}
