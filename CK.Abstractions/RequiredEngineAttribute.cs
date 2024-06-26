using System;

namespace CK.Setup
{
    /// <summary>
    /// Assembly attribute that states that this assembly requires a CKomposable Engine to be setup.
    /// This is typically used by PFeatures definers (see <see cref="IsPFeatureDefinerAttribute"/>) to declare one
    /// or more associated engines.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
    public sealed class RequiredEngineAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new required engine dependency attribute.
        /// </summary>
        /// <param name="assemblyName">Name of the CKomposable Engine assembly.</param>
        /// <param name="versionBound">
        /// A NuGet version range (see https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort#version-ranges)
        /// of the Engine to use.
        /// </param>
        public RequiredEngineAttribute( string assemblyName, string versionBound )
        {
        }
    }
}
