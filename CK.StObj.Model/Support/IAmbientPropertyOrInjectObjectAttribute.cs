namespace CK.Setup
{
    /// <summary>
    /// Unifies <see cref="Core.AmbientPropertyAttribute"/> and <see cref="Core.InjectObjectAttribute"/>.
    /// </summary>
    public interface IAmbientPropertyOrInjectObjectAttribute
    {
        /// <summary>
        /// Gets whether resolving this property is required or not.
        /// </summary>
        bool IsOptional { get; }

        /// <summary>
        /// Gets whether that attribute defines the <see cref="IsOptional"/> value or if it must be inherited.
        /// </summary>
        bool IsOptionalDefined { get; }

        /// <summary>
        /// Gets whether the property is an ambient property. Otherwise it is an injected singleton.
        /// </summary>
        bool IsAmbientProperty { get; }
    }
}
