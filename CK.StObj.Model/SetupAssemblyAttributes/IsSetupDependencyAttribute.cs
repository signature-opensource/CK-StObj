using System;


namespace CK.Setup
{
    /// <summary>
    /// Marks an assembly as being a setup dependency.
    /// A setup dependency can have <see cref="RequiredSetupDependencyAttribute"/> just like Models.
    /// <para>
    /// This assembly attribute, just like <see cref="ExcludeFromSetupAttribute"/>, <see cref="IsModelAttribute"/> and <see cref="RequiredSetupDependencyAttribute"/>)
    /// is defined here so that dependent assemblies can easily apply them on their own assemblies but they are used by CKSetup for which the full
    /// name is enough (duck typing): any CK.Setup.IsSetupDependencyAttribute attribute, even locally defined will do the job.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
    public class IsSetupDependencyAttribute : Attribute
    {
    }
}
