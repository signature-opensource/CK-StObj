namespace CK.Setup
{
    public interface IExtTypeInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the base type info if there is one.
        /// </summary>
        IExtTypeInfo? BaseType { get; }
    }
}
