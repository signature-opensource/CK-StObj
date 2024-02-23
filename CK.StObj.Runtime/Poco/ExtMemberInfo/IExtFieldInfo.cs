using System.Reflection;

namespace CK.Setup
{
    public interface IExtFieldInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the field info.
        /// </summary>
        FieldInfo FieldInfo { get; }
    }
}
