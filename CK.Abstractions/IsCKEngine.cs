using System;

namespace CK.Setup
{
    /// <summary>
    /// Marks an assembly as being an engine.
    /// <para>
    /// Being a CKEngine is transitive: assemblies that evenually depends on an engine assembly
    /// are also engine assemblies.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
    public sealed class IsCKEngineAttribute : Attribute
    {
    }

}
