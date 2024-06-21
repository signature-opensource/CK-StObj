using CK.Setup;

namespace CK.Engine.TypeCollector
{
    public enum AssemblyKind
    {
        /// <summary>
        /// This assembly is unknown or non relevant: it references no <see cref="PFeatureDefiner"/> nor <see cref="PFeature"/>
        /// and is not a CKEngine.
        /// </summary>
        None,

        /// <summary>
        /// This assembly has been skipped. It is a system assembly that we ignore.
        /// </summary>
        Skipped,

        /// <summary>
        /// This assembly has been excluded by configuration.
        /// </summary>
        Excluded,

        /// <summary>
        /// This assembly is on the engine side.
        /// </summary>
        CKEngine,

        /// <summary>
        /// This is a definer assembly (it is marke with a <see cref="IsPFeatureAttribute"/>).
        /// Assemblies that reference it are <see cref="PFeature"/>.
        /// </summary>
        PFeatureDefiner,

        /// <summary>
        /// The assembly's visible types will be registered: it is either marked with a <see cref="IsPFeatureAttribute"/>
        /// or depends (at any depth) on another PFeature or an assembly that is marked with a <see cref="IsPFeatureDefinerAttribute"/>.
        /// </summary>
        PFeature
    }
}
