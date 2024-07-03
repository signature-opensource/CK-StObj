using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    sealed class ExtEventInfo : ExtMemberInfoBase, IExtEventInfo
    {
        public ExtEventInfo( ExtMemberInfoFactory factory, EventInfo p )
            : base( factory, p )
        {
        }

        public EventInfo EventInfo => Unsafe.As<EventInfo>( _o );
    }

}
