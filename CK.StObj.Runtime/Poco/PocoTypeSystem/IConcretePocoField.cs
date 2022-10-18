namespace CK.Setup
{
    /// <summary>
    /// Field of <see cref="IConcretePocoType"/>.
    /// </summary>
    public interface IConcretePocoField : IPocoField
    {
        /// <summary>
        /// Gets the property info (with all its <see cref="IPocoPropertyInfo.DeclaredProperties"/>).
        /// </summary>
        IPocoPropertyInfo Property { get; }

        /// <summary>
        /// Gets the private generated field name: "_v{Index}".
        /// </summary>
        string PrivateFieldName { get; }

    }
}
