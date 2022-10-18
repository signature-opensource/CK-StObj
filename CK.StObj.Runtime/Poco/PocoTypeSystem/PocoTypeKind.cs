namespace CK.Setup
{

    /// <summary>
    /// </summary>
    public enum PocoTypeKind
    {
        /// <summary>
        /// Invalid or unknown type.
        /// </summary>
        None,

        /// <summary>
        /// Any object: this is the <see cref="object"/> type that generalizes
        /// any Poco type.
        /// </summary>
        Any,

        /// <summary>
        /// Basic types are defined by <see cref="PocoSupportResultExtension.IsBasicPropertyType(Type)"/>.
        /// </summary>
        Basic,

        /// <summary>
        /// ValueTuple of Poco types (<see cref="IRecordPocoType"/>).
        /// </summary>
        AnonymousRecord,

        /// <summary>
        /// Fully mutable struct of Poco compliant types (<see cref="IRecordPocoType"/>).
        /// </summary>
        Record,

        /// <summary>
        /// A concrete <see cref="IPoco"/> (<see cref="IConcretePocoType"/>).
        /// </summary>
        IPoco,

        /// <summary>
        /// An abstract IPoco (<see cref="IAbstractPocoType"/>).
        /// </summary>
        AbstractIPoco,

        /// <summary>
        /// Enumeration (<see cref="IEnumPocoType"/>).
        /// </summary>
        Enum,

        /// <summary>
        /// An array of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        Array,

        /// <summary>
        /// A <c>List<></c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        List,

        /// <summary>
        /// A <c>HashSet<></c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        HashSet,

        /// <summary>
        /// A <c>Dictionary<,></c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        Dictionary
    }
}
