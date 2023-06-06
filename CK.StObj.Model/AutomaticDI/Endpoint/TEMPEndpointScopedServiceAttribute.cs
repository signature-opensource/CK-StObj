using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{
    /// <summary>
    /// States that the decorated class or interface is a scoped endpoint service.
    /// </summary>
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true, Inherited = false )]
    public sealed class TEMPEndpointScopedServiceAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new <see cref="EndpointScopedServiceAttribute"/>.
        /// </summary>
        /// <param name="isUbiquitousEndpointInfo">See <see cref="IsUbiquitousEndpointInfo"/>.</param>
        public TEMPEndpointScopedServiceAttribute( bool isUbiquitousEndpointInfo = false )
        {
            IsUbiquitousEndpointInfo = isUbiquitousEndpointInfo;
        }

        /// <summary>
        /// Gets whether this endpoint scoped service is a ubiquitous service that carries
        /// information that all endpoint must support.
        /// <para>
        /// Very few services are or can be such ubiquitous information:
        /// <list type="bullet">
        ///     <item>It must carry information of general interest.</item>
        ///     <item>It should be immutable (at least thread safe but there's little use of mutability here).</item>
        ///     <item>When not resolvable, a sensible (non null) default must exist.</item>
        /// </list>
        /// </para>
        /// </summary>
        public bool IsUbiquitousEndpointInfo { get; }
    }
}
