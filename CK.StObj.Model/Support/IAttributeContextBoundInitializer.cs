using System;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IAttributeContextBound"/> in order to be initialized with the <see cref="MemberInfo"/> that is
    /// decorated with the attribute and the whole type information (giving access to any other attributes on the type).
    /// <para>
    /// Delegated attributes initialized by <see cref="ContextBoundDelegationAttribute"/> can also use their constructor:
    /// the <see cref="ICKCustomAttributeTypeMultiProvider"/>, <see cref="Type"/> or <see cref="MemberInfo"/> parameters
    /// will be injected. There is however one subtke difference: when constructor injection is used, the <see cref="ICKCustomAttributeTypeMultiProvider"/>
    /// will be "empty". To access other attributes on the owner, this interface must be implemented.
    /// </para>
    /// </summary>
    public interface IAttributeContextBoundInitializer : IAttributeContextBound
    {
        /// <summary>
        /// Called the first time the attribute is obtained.
        /// </summary>
        /// <param name="owner">The <see cref="ICKCustomAttributeTypeMultiProvider"/> that gives access to all the types' attributes.</param>
        /// <param name="m">The member that is decorated by this attribute.</param>
        void Initialize( ICKCustomAttributeTypeMultiProvider owner, MemberInfo m );
    }
    
}
