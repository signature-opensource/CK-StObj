using System.Diagnostics.CodeAnalysis;

namespace CK.Core
{
    /// <summary>
    /// Endpoint specific data provider.
    /// Endpoint scoped services depend on it to expose any endpoint instance or resolution context specific information.
    /// </summary>
    /// <typeparam name="TScopeData">Data specific to the endpoint from which endpoint scoped services can be derived.</typeparam>
    public sealed class EndpointScopeData<TScopeData> where TScopeData : notnull
    {
        [AllowNull]
        internal TScopeData _data;

        /// <summary>
        /// Gets the endpoint instance data.
        /// </summary>
        public TScopeData Data => _data;
    }

}
