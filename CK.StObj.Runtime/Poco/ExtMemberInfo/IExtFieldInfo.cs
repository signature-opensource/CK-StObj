using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IExtMemberInfo"/>.
    /// </summary>
    public interface IExtFieldInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the field info.
        /// </summary>
        FieldInfo FieldInfo { get; }
    }
}
