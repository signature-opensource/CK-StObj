namespace CK.Setup
{
    /// <summary>
    /// Common type for <see cref="IConcretePocoField"/> and <see cref="IRecordPocoField"/>.
    /// </summary>
    public interface IPocoField
    {
        /// <summary>
        /// Gets the index of this field or property in the <see cref="IPocoType.Fields"/>.
        /// Indexes starts at 0 and are compact: this can be used to handle optimized serialization
        /// by index (MessagePack) rather than by name (Json).
        /// <para>
        /// The generated backing field is named <c>_v{Index}</c> in IPoco generated code.
        /// </para>
        /// </summary>
        int Index { get; }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the field type.
        /// </summary>
        IPocoType Type { get; }

        /// <summary>
        /// Gets whether this field is disallowed in a owner, always allowed or
        /// allowed but requires the <see cref="DefaultValueInfo.DefaultValue"/> to be set.
        /// </summary>
        DefaultValueInfo DefaultValueInfo { get; }
    }
}
