using System;

namespace CK.Setup
{
    /// <summary>
    /// Marks an assembly so that any assemblies that references it are <see cref="IsCKAssemblyAttribute"/>.
    /// A "definer" typically defines abstract constructs and attributes that are referenced and used by CKAssemblies
    /// but doesn't need to be processed because it doesn't contain types that must directly participate to the setup process.
    /// <para>
    /// Definers are usually also marked with a <see cref="RequiredSetupDependencyAttribute"/> that identifies an associated engine
    /// component that handles the final implementation. 
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
    public sealed class IsCKAssemblyDefinerAttribute : Attribute
    {
    }

}
