using CK.Core;
using System;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IAttributeContextBound"/> in order to be initialized with the <see cref="MemberInfo"/> that is
    /// decorated with the attribute and the whole type information (giving access to any other attributes on the type).
    /// <para>
    /// Delegated attributes initialized by <see cref="ContextBoundDelegationAttribute"/> can also use their constructor:
    /// the <see cref="ITypeAttributesCache"/>, <see cref="Type"/> or <see cref="MemberInfo"/> parameters
    /// will be injected. There is however one subtle difference: when constructor injection is used, the <see cref="ITypeAttributesCache"/>
    /// will be "empty".
    /// To access other attributes as early as possible on the owner, this interface must be implemented.
    /// </para>
    /// </summary>
    public interface IAttributeContextBoundInitializer : IAttributeContextBound
    {
        /// <summary>
        /// Called once all the attributes have been discovered.
        /// </summary>
        /// <param name="monitor">The monitor to use. Any error or fatal logged will abort the process after the types discovering phase.</param>
        /// <param name="owner">The <see cref="ITypeAttributesCache"/> that gives access to all the types' attributes.</param>
        /// <param name="m">The member that is decorated by this attribute.</param>
        /// <param name="alsoRegister">Enables this method to register types (typically nested types).</param>
        void Initialize( IActivityMonitor monitor, ITypeAttributesCache owner, MemberInfo m, Action<Type> alsoRegister );
    }
    
}
