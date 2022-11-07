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
        /// Basic types are int, long, short, byte, string, bool, double, float, DateTime, DateTimeOffset, TimeSpan,
        /// Guid, decimal, BigInteger, uint, ulong, ushort, sbyte.
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
        /// A concrete <see cref="IPoco"/> (<see cref="IPrimaryPocoType"/>).
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
        /// A <c>List&lt;&gt;</c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        List,

        /// <summary>
        /// A <c>HashSet&lt;&gt;</c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        HashSet,

        /// <summary>
        /// A <c>Dictionary&lt;,&gt;</c> of Poco type (<see cref="ICollectionPocoType"/>).
        /// </summary>
        Dictionary,

        /// <summary>
        /// An <c>object</c> constrained to a set of allowed types (<see cref="IUnionPocoType"/>).
        /// </summary>
        UnionType
    }
}
