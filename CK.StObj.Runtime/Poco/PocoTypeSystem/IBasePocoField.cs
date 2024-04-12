namespace CK.Setup
{
    /// <summary>
    /// Unifies <see cref="IAbstractPocoField"/> and <see cref="IPocoField"/> (that itself unifies <see cref="IPrimaryPocoField"/>
    /// and <see cref="IRecordPocoField"/>).
    /// </summary>
    public interface IBasePocoField
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
