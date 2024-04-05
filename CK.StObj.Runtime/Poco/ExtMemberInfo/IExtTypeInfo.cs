namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IExtMemberInfo"/>.
    /// </summary>
    public interface IExtTypeInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the base type info if there is one.
        /// </summary>
        IExtTypeInfo? BaseType { get; }
    }
}
