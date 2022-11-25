using System.Reflection;

namespace CK.Setup
{
    sealed class ExtParameterInfo : ExtMemberInfoBase, IExtParameterInfo
    {
        public ExtParameterInfo( ExtMemberInfoFactory factory, ParameterInfo p )
            : base( factory, p )
        {
        }

        public ParameterInfo ParameterInfo => (ParameterInfo)_o;
    }

}
