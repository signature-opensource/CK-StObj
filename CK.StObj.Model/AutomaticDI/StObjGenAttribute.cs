using System;

namespace CK.Core
{
    /// <summary>
    /// Marks any code construct as a code generation artifact.
    /// Code generator should define this attribute on any generated code.
    /// </summary>
    /// <remarks>
    /// This should be "duck typed" in the sense where any "StObjGenAttribute" in
    /// any namespace must be enough to conclude that the code construct has been generated.
    /// </remarks>
    [AttributeUsage( AttributeTargets.All, AllowMultiple = true, Inherited = true )]
    public class StObjGenAttribute : Attribute
    {
    }

}
