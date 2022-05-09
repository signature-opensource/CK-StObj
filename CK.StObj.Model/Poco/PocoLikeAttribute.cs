using System;

namespace CK.Core
{
    /// <summary>
    /// Marks a class as being a IPoco like object:
    /// <list type="bullet">
    ///  <item>It must be a public class with a default public constructor.</item>
    ///  <item>It can expose public read only non null List&lt;&gt;, Dictionary&lt;,&gt;, HashSet&lt;&gt; (initialized in the constructor).</item>
    ///  <item>It may be abstract.</item>
    /// </list>
    /// A Poco-like object is aimed to be exchanged, serialized and deserialized just like IPoco.
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
