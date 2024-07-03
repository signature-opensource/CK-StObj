using CK.Core;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CK.Setup
{
    /// <summary>
    /// Implements extensions on <see cref="IExtMemberInfo"/>.
    /// </summary>
    public static class ExtMemberInfoExtensions
    {
        /// <summary>
        /// Tries to compute the nullability info of this member.
        /// This checks that the <see cref="NullabilityInfo.ReadState"/> is the same as
        /// the <see cref="NullabilityInfo.WriteState"/>: no [AllowNull], [DisallowNull] or
        /// other nullability attributes must exist: an error log is emitted in such case.
        /// </summary>
        /// <param name="this">This member info.</param>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>Null if Read/Write nullabilities differ.</returns>
        public static IExtNullabilityInfo? GetHomogeneousNullabilityInfo( this IExtMemberInfo @this, IActivityMonitor monitor )
        {
            var r = @this.ReadNullabilityInfo;
            if( !r.IsHomogeneous )
            {
                monitor.Error( $"Read/Write nullabilities differ for {@this.ToString()}. No [AllowNull], [DisallowNull] or other nullability attributes should be used." );
                return null;
            }
            return r;
        }

        /// <summary>
        /// Filters the custom attributes of this member.
        /// </summary>
        public static IEnumerable<T> GetCustomAttributes<T>( this IExtMemberInfo @this ) => @this.CustomAttributes.OfType<T>();



    }
}
