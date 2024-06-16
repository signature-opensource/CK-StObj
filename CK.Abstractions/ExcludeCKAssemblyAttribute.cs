using System;

namespace CK.Setup
{
    /// <summary>
    /// Prevents the specified referenced assembly's types to be considered when registering this assembly.
    /// <para>
    /// This is a "weak" hiding: types explicitly referenced from this assembly will be registered.
    /// This allows to hide all the types of a referenced assembly by default and to opt-in exposing some of
    /// their types by using <see cref="ExportCKTypeAttribute"/>.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
    public sealed class ExcludeCKAssemblyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="ExcludeCKAssemblyAttribute"/>. Note that the assembly name
        /// is checked: if it is not found in this assembly's references, a setup error occurs.
        /// </summary>
        /// <param name="assemblyName">The simple assembly name.</param>
        public ExcludeCKAssemblyAttribute( string assemblyName )
        {
            AssemblyName = assemblyName;
        }

        /// <summary>
        /// Gets the referenced assembly name for which types must not be automatically registered.
        /// </summary>
        public string AssemblyName { get; }
    }

}
