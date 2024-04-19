namespace CK.Core
{
    /// <summary>
    /// Categorizes the two possible kind of endpoints.
    /// </summary>
    public enum DIContainerKind
    {
        /// <summary>
        /// This kind of containers are called from another container.
        /// <para>
        /// These containers don't need to know all the Ambient services that may exist in a system:
        /// they are free to configure their instaances or to use the <see cref="IDIContainer.ScopeDataType"/>
        /// and the caller <see cref="AmbientServiceHub"/> to transfer the calling Ambient services.
        /// </para>
        /// </summary>
        Backend,

        /// <summary>
        /// This kind of containers are called "out of the blue": no existing DI context exists, they must
        /// configure all the required services including Ambient services without relying
        /// on the <see cref="AmbientServiceHub"/>.
        /// <para>
        /// These containers don't need to know all the Ambient services that may exist in a system:
        /// Non configured Ambient services are automatically configured by using their <see cref="IEndpointUbiquitousServiceDefault{T}"/> companion.
        /// </para>
        /// </summary>
        Endpoint
    }

}
