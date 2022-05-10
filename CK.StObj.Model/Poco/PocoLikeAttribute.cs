using System;

namespace CK.Core
{
    /// <summary>
    /// Marks a class as being a IPoco like object:
    /// <list type="bullet">
    ///  <item>It must be a either a public abstract class or a public concrete class with a default public constructor.</item>
    ///  <item>Its public properties must be of Poco compliant types.</item>
    /// </list>
    /// A Poco-like object behaves like a IPoco.
    /// <para>
    /// This attribute is not inherited: each type in a hierarchy must be explicitly decorated with it. 
    /// </para>
    /// <para>
    /// It is not required to be this exact type: any attribute
    /// named "PocoLikeAttribute" defined in any namespace will be considered.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PocoLikeAttribute : Attribute
    {
    }
}
