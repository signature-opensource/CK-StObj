namespace CK.Setup
{
    /// <summary>
    /// Field of <see cref="IPrimaryPocoType"/>.
    /// When this <see cref="IPocoType.ITypeRef.Type"/> is a <see cref="PocoTypeKind.AnonymousRecord"/>
    /// or a <see cref="PocoTypeKind.Record"/> this is a ref property.
    /// </summary>
    public interface IPrimaryPocoField : IPocoField
    {
        /// <inheritdoc cref="IPocoField.Owner"/>
        new IPrimaryPocoType Owner { get; }

        /// <summary>
        /// Gets the property info (with all its <see cref="IPocoPropertyInfo.DeclaredProperties"/>).
        /// </summary>
        IPocoPropertyInfo Property { get; }

        /// <summary>
        /// Gets the <see cref="PocoFieldAccessKind"/> for this field.
        /// </summary>
        PocoFieldAccessKind FieldAccess { get; }

        /// <summary>
        /// Gets the private generated field name: "_v{Index}".
        /// </summary>
        string PrivateFieldName { get; }
    }
}
