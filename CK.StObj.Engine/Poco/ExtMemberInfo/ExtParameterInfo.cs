using System.Reflection;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    sealed class ExtParameterInfo : ExtMemberInfoBase, IExtParameterInfo
    {
        public ExtParameterInfo( ExtMemberInfoFactory factory, ParameterInfo p )
            : base( factory, p )
        {
        }

        public ParameterInfo ParameterInfo => Unsafe.As<ParameterInfo>( _o );
    }

}
