namespace CK.Setup
{
    /// <summary>
    /// Common field attributes for <see cref="IPrimaryPocoField"/> and <see cref="IRecordPocoField"/>.
    /// </summary>
    public interface IPocoField : IPocoType.ITypeRef
    {
        /// <summary>
        /// Gets the owner of this field.
        /// </summary>
        new ICompositePocoType Owner { get; }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the field type name. It may not be the actual field type.
        /// </summary>
        string FieldTypeCSharpName { get; }

        /// <summary>
        /// Gets whether this field is disallowed in a owner, always allowed or
        /// allowed but requires the <see cref="DefaultValueInfo.DefaultValue"/> to be set.
        /// </summary>
        DefaultValueInfo DefaultValueInfo { get; }

        /// <summary>
        /// Gets whether this field is exchangeable: the field type must be exchangeable
        /// and for <see cref="IPrimaryPocoField"/>, <see cref="IPrimaryPocoField.HasSetter"/>
        /// or <see cref="IPrimaryPocoField.IsByRef"/> must be true.
        /// </summary>
        bool IsExchangeable { get; }

    }
}
