namespace CK.Setup
{
    /// <summary>
    /// Mutually exclusive allowed types of poco property.
    /// </summary>
    public enum PocoPropertyKind
    {
        /// <summary>
        /// Invalid or unknown type.
        /// </summary>
        None,

        /// <summary>
        /// A basic property type: see <see cref="PocoSupportResultExtension.IsBasicPropertyType(Type)"/>.
        /// </summary>
        Basic,

        /// <summary>
        /// A IPoco interface.
        /// </summary>
        IPoco,

        /// <summary>
        /// A Poco-like class.
        /// </summary>
        PocoClass,

        /// <summary>
        /// Standard collection are an array, HashSet&lt;&gt;,
        /// List&lt;&gt;, or Dictionary&lt;,&gt; of (potentially recursive) PocoPropertyType.
        /// </summary>
        StandardCollection,

        /// <summary>
        /// Union (algebraic type) of one (weird!) or more PocoPropertyType.
        /// Such properties are only supported by IPoco, not PocoClass.
        /// </summary>
        Union,

        /// <summary>
        /// Tuple of PocoPropertyType.
        /// </summary>
        ValueTuple,

        /// <summary>
        /// Enumeration.
        /// </summary>
        Enum,

        /// <summary>
        /// Any object: this is the <see cref="object"/> type that generalizes
        /// an Poco compliant types.
        /// </summary>
        Any
    }
}
