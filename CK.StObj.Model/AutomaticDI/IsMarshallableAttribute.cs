using System;

namespace CK.Core
{
    /// <summary>
    /// Marks a class or an interface as a marshallable one: there must be a way to capture the service state and
    /// transfer (marshall) its state in another context, domain or process.
    /// This attribute applies to the type it decorates and only it, it doesn't propagate to any specialization.
    /// <para>
    /// It is not required to be this exact type: any attribute named "IsMarshallableAttribute" defined in any
    /// namespace will be considered as a valid marker.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false, Inherited = false )]
    public class IsMarshallableAttribute : Attribute
    {
    }

}
