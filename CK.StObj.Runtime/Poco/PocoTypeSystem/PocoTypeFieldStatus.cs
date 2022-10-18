namespace CK.Setup
{
    /// <summary>
    /// Qualifies a <see cref="IPocoType"/> with regard to its usage in the
    /// Poco type system.
    /// </summary>
    public enum PocoTypeFieldStatus
    {
        /// <summary>
        /// The type can only be used externally, as a field or property of
        /// a <see cref="IRecordPocoType"/> that appear in a collection.
        /// <para>
        /// The type cannot be instantiated without violating its constraint.
        /// For instance, int the value tuple <c>(IPoco? A, IPoco B)</c>, B cannot
        /// be null and cannot be resolved to a non null instance: this tuple is
        /// initially invalid.
        /// </para>
        /// </summary>
        Disallowed,

        /// <summary>
        /// The type can be used as a <see cref="IConcretePocoField"/> or <see cref="IRecordPocoType"/>
        /// field without any initialization.
        /// All nullable types are "Allowed", they will be initialized to null.
        /// </summary>
        Allowed,

        /// <summary>
        /// The type can be used as a <see cref="IConcretePocoField"/> or <see cref="IRecordPocoType"/>
        /// field. The type is not nullable and requires an initialization: either a <see cref="IPocoFieldDefaultValue"/>
        /// exists or the type is a concrete one.
        /// </summary>
        RequiresInit
    }
}
