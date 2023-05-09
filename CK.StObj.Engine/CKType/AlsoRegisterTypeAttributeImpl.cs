using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    public sealed class AlsoRegisterTypeAttributeImpl : IAttributeContextBoundInitializer
    {
        readonly Type _type;

        public AlsoRegisterTypeAttributeImpl( AlsoRegisterTypeAttribute a )
        {
            _type = a.Type;
        }

        void IAttributeContextBoundInitializer.Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister )
        {
            alsoRegister( _type );
        }

    }
}