using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IExtMemberInfo"/>.
    /// </summary>
    public interface IExtEventInfo : IExtMemberInfo
    {
        /// <summary>
        /// Gets the event info.
        /// </summary>
        EventInfo EventInfo { get; }
    }
}
