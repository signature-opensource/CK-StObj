namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" dictionary for AbstractPoco values.
    /// </summary>
    public interface IPocoDictionaryOfAbstractOrBasicRefRequiredSupport : IPocoRequiredSupportType
    {
        /// <summary>
        /// Gets the necessary non nullable key type.
        /// </summary>
        IPocoType KeyType { get; }

        /// <summary>
        /// Gets the non nullable item type: a <see cref="IBasicRefPocoType"/> or <see cref="IAbstractPocoType"/>.
        /// </summary>
        IPocoType ValueType { get; }
    }
}
