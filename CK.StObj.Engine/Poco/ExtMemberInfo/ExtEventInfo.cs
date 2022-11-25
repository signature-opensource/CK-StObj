using System.Reflection;

namespace CK.Setup
{
    sealed class ExtEventInfo : ExtMemberInfoBase, IExtEventInfo
    {
        public ExtEventInfo( ExtMemberInfoFactory factory, EventInfo p )
            : base( factory, p )
        {
        }

        public EventInfo EventInfo => (EventInfo)_o;
    }

}
