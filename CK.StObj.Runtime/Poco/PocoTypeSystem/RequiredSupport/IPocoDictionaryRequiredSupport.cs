namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" dictionary for a Poco.
    /// </summary>
    public interface IPocoDictionaryRequiredSupport : IPocoRequiredSupportType
    {
        /// <summary>
        /// Gets the necessary non nullable key type.
        /// </summary>
        IPocoType KeyType { get; }

        /// <summary>
        /// Gets the not nullable value type.
        /// </summary>
        IPrimaryPocoType ValueType { get; }
    }
}
