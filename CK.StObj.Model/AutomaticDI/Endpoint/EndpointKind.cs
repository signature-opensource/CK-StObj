namespace CK.Core
{
    /// <summary>
    /// Categorizes the two possible kind of endpoints.
    /// </summary>
    public enum EndpointKind
    {
        /// <summary>
        /// This kind of endpoints are called from another endpoint: a service provider
        /// is available and all ubiquitous information services are resolved (even if it
        /// is with their default values provided by <see cref="IEndpointUbiquitousServiceDefault{T}"/>
        /// <para>
        /// Back endpoint resolves non explicitly mapped ubiquitous information services
        /// from the <see cref="EndpointUbiquitousInfo"/> scoped instance.
        /// </para>
        /// </summary>
        Back,

        /// <summary>
        /// This kind of endpoints are called "out of the blue": no existing DI context exists, they must
        /// resolve all the required services including ubiquitous information services without relying
        /// on the <see cref="EndpointUbiquitousInfo"/>.
        /// <para>
        /// Back endpoint resolves non explicitly mapped ubiquitous information services
        /// from the <see cref="IEndpointUbiquitousServiceDefault{T}"/> singleton instances.
        /// </para>
        /// </summary>
        Front
    }

}
