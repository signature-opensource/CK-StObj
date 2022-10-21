namespace CK.Setup
{
    /// <summary>
    /// Field of <see cref="IConcretePocoType"/>.
    /// Note that when this <see cref="IPocoField.Type"/> is a <see cref="PocoTypeKind.AnonymousRecord"/>
    /// or a <see cref="PocoTypeKind.Record"/> this is a ref property.
    /// </summary>
    public interface IConcretePocoField : IPocoField
    {
        /// <summary>
        /// Gets the property info (with all its <see cref="IPocoPropertyInfo.DeclaredProperties"/>).
        /// </summary>
        IPocoPropertyInfo Property { get; }

        /// <summary>
        /// Gets whether this field is read only.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets whether this is a ref property.
        /// </summary>
        bool IsByRef { get; }

        /// <summary>
        /// Gets the private generated field name: "_v{Index}".
        /// </summary>
        string PrivateFieldName { get; }

    }
}
