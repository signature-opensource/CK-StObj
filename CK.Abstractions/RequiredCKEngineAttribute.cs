using System;

namespace CK.Setup
{
    /// <summary>
    /// Assembly attribute that states that this assembly requires a CKEngine to be setup.
    /// This is typically used by PFeatures definers (see <see cref="IsPFeatureDefinerAttribute"/>) to declare one
    /// or more associated engines.
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = true )]
    public sealed class RequiredCKEngineAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new required CKEngine dependency attribute.
        /// </summary>
        /// <param name="assemblyName">Name of the CKEngine assembly.</param>
        /// <param name="versionBound">
        /// A NuGet version range (see https://learn.microsoft.com/en-us/nuget/concepts/package-versioning?tabs=semver20sort#version-ranges)
        /// of the CKEngine to use.
        /// </param>
        public RequiredCKEngineAttribute( string assemblyName, string versionBound )
        {
            AssemblyName = assemblyName;
            VersionBound = versionBound;
        }

        /// <summary>
        /// Gets the simple name of the CKEngine assembly.
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Gets the NuGet version range of the CKEngine to use.
        /// </summary>
        public string VersionBound { get; }
    }
}
