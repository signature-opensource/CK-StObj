using System.Reflection;

namespace CK.Setup
{
    public interface IExtPropertyInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the property info.
        /// </summary>
        PropertyInfo PropertyInfo { get; }
    }
}
