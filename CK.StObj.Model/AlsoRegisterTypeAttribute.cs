using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Setup
{
    /// <summary>
    /// Enables any registered type to register another type.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    public class AlsoRegisterTypeAttribute : ContextBoundDelegationAttribute
    {
        /// <summary>
        /// Initializes a <see cref="AlsoRegisterTypeAttribute"/> with a type
        /// that must be registered.
        /// </summary>
        /// <param name="type">A type (typically a nested type) that must be registered.</param>
        public AlsoRegisterTypeAttribute( Type type )
            : base( "CK.Setup.AlsoRegisterTypeAttributeImpl, CK.StObj.Engine" )
        {
            Type = type;
        }

        /// <summary>
        /// Gets the type that must be registered.
        /// </summary>
        public Type Type { get; }
    }
}
