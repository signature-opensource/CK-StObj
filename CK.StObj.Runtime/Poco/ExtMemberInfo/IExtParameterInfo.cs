using System.Reflection;

namespace CK.Setup
{
    public interface IExtParameterInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the parameter info.
        /// </summary>
        ParameterInfo ParameterInfo { get; }
    }
}
