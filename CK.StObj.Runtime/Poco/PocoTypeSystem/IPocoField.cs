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
        /// Gets the default value if a <see cref="System.ComponentModel.DefaultValueAttribute"/> is defined
        /// or if a positional parameter of a record struct has a default value.
        /// <para>
        /// For IPoco, when the default value is defined by more than one IPoco interface, it must be the same.
        /// </para>
        /// </summary>
        IPocoFieldDefaultValue? DefaultValue { get; }
    }
}
