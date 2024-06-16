namespace CK.Setup
{
    public enum CKAssemblyKind
    {
        /// <summary>
        /// This assembly is unknown or non relevant: it references no <see cref="CKAssemblyDefiner"/> nor <see cref="CKAssembly"/>
        /// assemblies.
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
        /// <para>
        /// An engine assembly is ignored.
        /// </para>
        /// </summary>
        CKEngine,

        /// <summary>
        /// This is a definer assembly (it is marke with a <see cref="IsModelAttribute"/>).
        /// Assemblies that reference it are <see cref="CKAssembly"/>.
        /// <para>
        /// A definer assembly is ignored.
        /// </para>
        /// </summary>
        CKAssemblyDefiner,

        /// <summary>
        /// The assembly's visible types will be registered: it is either marked with a <see cref="IsModelDependentAttribute"/>
        /// or depends (at any depth) on another CKAssembly or an assembly that is marked with a <see cref="IsModelAttribute"/>.
        /// </summary>
        CKAssembly
    }
}
