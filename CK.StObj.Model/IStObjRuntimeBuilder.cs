using System;

namespace CK.Core
{
    /// <summary>
    /// Objects that implement this interface are in charge of actual creation and final configuration/injection
    /// of the built objects.
    /// It is used both at build time and at run time.
    /// </summary>
    public interface IStObjRuntimeBuilder
    {
        /// <summary>
        /// First method called when an instance of the final type must be instantiated.
        /// This can use any one of the constructors offered by the <paramref name="finalType"/>. 
        /// When the finalType is an automatically generated concrete class, all the protected and public constructors are redefined
        /// with all of their attributes and all attributes of their parameters replicated: a DI container can rely on them
        /// to adapt its behavior.
        /// </summary>
        /// <param name="finalType">Final type of the object to create. Can be an automatically generated concrete type that specializes an abstract one.</param>
        /// <returns>Must return a non null instance of the provided type.</returns>
        object CreateInstance( Type finalType );

    }
}
