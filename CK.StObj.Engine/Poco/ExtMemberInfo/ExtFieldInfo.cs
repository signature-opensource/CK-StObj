using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    sealed class ExtFieldInfo : ExtMemberInfoBase, IExtFieldInfo
    {
        public ExtFieldInfo( ExtMemberInfoFactory factory, FieldInfo p )
            : base( factory, p )
        {
        }

        public FieldInfo FieldInfo => Unsafe.As<FieldInfo>( _o );
    }

}
