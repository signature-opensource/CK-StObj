using System.Reflection;

namespace CK.Setup
{
    public interface IExtEventInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the event info.
        /// </summary>
        EventInfo EventInfo { get; }
    }
}
