using System;

namespace CK.Core
{
    /// <summary>
    /// When applied on an abstract class, prevents any kind of auto implementation for this 
    /// exact type: this is a kind of "really abstract" marker for potentially implementable abstract classes. 
    /// <para>
    /// This attribute, just like <see cref="IRealObject"/>, <see cref="IAutoService"/>, <see cref="IScopedAutoService"/>, 
    /// <see cref="ISingletonAutoService"/> or <see cref="ReplaceAutoServiceAttribute"/> can be created anywhere: as long as the
    /// name is "PreventAutoImplementationAttribute" (regardless of the namespace), it will be honored.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Class, AllowMultiple = false, Inherited = false )]
    public class PreventAutoImplementationAttribute : Attribute
    {
    }
}


