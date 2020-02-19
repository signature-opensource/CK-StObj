using System;


namespace CK.Setup
{
    /// <summary>
    /// Marks an assembly as being a a Model.
    /// <para>
    /// This assembly attribute, just like <see cref="ExcludeFromSetupAttribute"/>, <see cref="IsSetupDependencyAttribute"/> and <see cref="RequiredSetupDependencyAttribute"/>)
    /// is defined here so that dependent assemblies can easily apply them on their own assemblies but they are used by CKSetup for which the full
    /// name is enough (duck typing): any CK.Setup.IsModelAttribute attribute, even locally defined will do the job.
    /// </para>
    /// </summary>
    [AttributeUsage( AttributeTargets.Assembly, AllowMultiple = false )]
    public class IsModelAttribute : Attribute
    {
    }
}
