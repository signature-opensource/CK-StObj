using System;

namespace CK.Setup
{
    /// <summary>
    /// Marks an assembly as being a "CKAssembly". The types it contains must be processed by the setup.
    /// <para>
    /// This attribute doesn't need to be defined if the assembly eventually depends on an assembly that is
    /// marked with <see cref="IsCKAssemblyDefinerAttribute"/> because being a CKAssembly is transitive: assemblies
    /// that evenually depends on a CKAssembly are also CKAssemblies.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
    public sealed class IsCKAssemblyAttribute : Attribute
    {
    }

}
