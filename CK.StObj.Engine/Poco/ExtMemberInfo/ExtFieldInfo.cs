using System.Reflection;

namespace CK.Setup
{
    sealed class ExtFieldInfo : ExtMemberInfoBase, IExtFieldInfo
    {
        public ExtFieldInfo( ExtMemberInfoFactory factory, FieldInfo p )
            : base( factory, p )
        {
        }

        public FieldInfo FieldInfo => (FieldInfo)_o;
    }

}
