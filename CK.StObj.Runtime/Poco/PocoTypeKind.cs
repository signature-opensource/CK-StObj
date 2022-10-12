namespace CK.Setup
{
    /// <summary>
    /// The closed set of types managed by Poco.
    /// Extensibility relies on <see cref="IPoco"/> and <see cref="StandardCollection"/>.
    /// </summary>
    public enum PocoTypeKind
    {
        /// <summary>
        /// Invalid or unknown type.
        /// </summary>
        None,

        /// <summary>
        /// Basic types are defined by <see cref="PocoSupportResultExtension.IsBasicPropertyType(Type)"/>.
        /// </summary>
        Basic,

        /// <summary>
        /// An interface derived from <see cref="IPoco"/>.
        /// </summary>
        IPoco,

        /// <summary>
        /// Standard collection are HashSet&lt;&gt;,
        /// List&lt;&gt;, or Dictionary&lt;,&gt; of (potentially recursive) Poco types.
        /// <para>
        /// Their read only interfaces IReadOnlySet&lt;&gt;,
        /// IReadOnlyList&lt;&gt;, IReadOnlyDictionary&lt;,&gt; but also IReadOnlyCollection&lt;&gt; and
        /// IEnumerable&lt;&gt; can be used in read only properties.
        /// </para>
        /// </summary>
        StandardCollection,

        /// <summary>
        /// Tuple of Poco types.
        /// </summary>
        ValueTuple,

        /// <summary>
        /// Enumeration.
        /// </summary>
        Enum,

        /// <summary>
        /// Any object: this is the <see cref="object"/> type that generalizes
        /// any Poco type.
        /// </summary>
        Any
    }
}
