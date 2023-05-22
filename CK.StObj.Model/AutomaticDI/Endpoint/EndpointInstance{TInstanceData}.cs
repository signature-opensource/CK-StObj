using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{
    /// <summary>
    /// A endpoint instance is configured by endpoint instance data. Endpoint scoped services
    /// depend on it to expose any endpoint specific information.
    /// </summary>
    /// <typeparam name="TInstanceData">Data specific to the endpoint from which endpoint scoped services can be derived.</typeparam>
    public sealed class EndpointInstance<TInstanceData> where TInstanceData : class
    {
        [AllowNull]
        internal TInstanceData _data;

        /// <summary>
        /// Gets the endpoint instance data.
        /// </summary>
        public TInstanceData Data => _data;
    }

}
