using System;
using System.Reflection;

namespace CK.Setup
{

    /// <summary>
    /// Bound attributes, thanks to the cache, makes delegation for maximal decoupling easy. This base class can be used
    /// by attributes that must appear on runtime objects to split their implementation into assemblies that will 
    /// be loaded only at setup time.
    /// <para>
    /// See <see cref="ActualAttributeTypeAssemblyQualifiedName"/> for a description of the delegated attribute.
    /// </para>
    /// </summary>
    public class ContextBoundDelegationAttribute : Attribute, IAttributeContextBound
    {
        /// <summary>
        /// Initializes a new <see cref="ContextBoundDelegationAttribute"/> that delegates its behaviors to another object.
        /// </summary>
        /// <param name="actualAttributeTypeAssemblyQualifiedName">Assembly Qualified Name of the object that will replace this attribute during setup.</param>
        public ContextBoundDelegationAttribute( string actualAttributeTypeAssemblyQualifiedName )
        {
            ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
        }

        /// <summary>
        /// Gets the Assembly Qualified Name of the object that will replace this attribute during setup.
        /// <para>
        /// This class must have a public constructor that can accept any service provided by the
        /// aspects (see <see cref="StObjEngineConfiguration.Aspects"/>), in addition to:
        /// <list type="bullet">
        ///   <item>a <see cref="MemberInfo"/> that is the decorated member.</item>
        ///   <item>a <see cref="Type"/> that is the Type that owns the decorated member.</item>
        ///   <item>
        ///   a <see cref="ITypeAttributesCache"/> that is the cache of all the attributes of the Type.
        ///   (Note that if access to other attributes is required, <see cref="IAttributeContextBoundInitializer"/> must be used.)
        ///   </item>
        /// </list>
        /// When a specialization of this <see cref="ContextBoundDelegationAttribute"/> is defined, the target class' constructor MUST
        /// define a parameter that is an instance of this specialized typed attribute.
        /// </para>
        /// </summary>
        public string ActualAttributeTypeAssemblyQualifiedName { get; private set; }
    }
}
