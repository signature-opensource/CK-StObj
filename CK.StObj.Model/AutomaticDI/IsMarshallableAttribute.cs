using System;

namespace CK.Core
{
    /// <summary>
    /// Marks a class as a marshallable one: there must be a way to capture the service state and
    /// transfer (marshall) its state in another context, domain or process.
    /// <para>
    /// It is not required to be this exact type: any attribute named "IsMarshallableAttribute" defined in any
    /// namespace will be considered as a valid marker.
    /// </para>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class IsMarshallableAttribute : Attribute
    {
    }

}
