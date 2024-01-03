namespace CK.Setup
{
    /// <summary>
    /// An abstract field appears in <see cref="IAbstractPocoType.Fields"/>.
    /// The exchangeability is the type's <see cref="IPocoType.IsExchangeable"/>.
    /// </summary>
    public interface IAbstractPocoField
    {
        /// <summary>
        /// Gets the field name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the field type.
        /// </summary>
        IPocoType Type { get; }
    }
}
