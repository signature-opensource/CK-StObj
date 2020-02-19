using System;

namespace CK.Setup
{

    /// <summary>
    /// Bound attributes, thanks to the cache, makes delegation for maximal decoupling easy. This base class can be used
    /// by attributes that must appear on runtime objects to split their implementation into assemblies that will 
    /// be loaded only at setup time.
    /// </summary>
    public abstract class ContextBoundDelegationAttribute : Attribute, IAttributeContextBound
    {
        /// <summary>
        /// Initializes a new <see cref="ContextBoundDelegationAttribute"/> that delegates its behaviors to another object.
        /// </summary>
        /// <param name="actualAttributeTypeAssemblyQualifiedName">Assembly Qualified Name of the object that will replace this attribute during setup.</param>
        protected ContextBoundDelegationAttribute( string actualAttributeTypeAssemblyQualifiedName )
        {
            ActualAttributeTypeAssemblyQualifiedName = actualAttributeTypeAssemblyQualifiedName;
        }

        /// <summary>
        /// Gets the Assembly Qualified Name of the object that will replace this attribute during setup.
        /// The targeted object must have a public constructor that takes this attribute as its only parameter.
        /// </summary>
        public string ActualAttributeTypeAssemblyQualifiedName { get; private set; }
    }
}
