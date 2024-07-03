using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IExtMemberInfo"/>.
    /// </summary>
    public interface IExtPropertyInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the property info.
        /// </summary>
        PropertyInfo PropertyInfo { get; }
    }
}
