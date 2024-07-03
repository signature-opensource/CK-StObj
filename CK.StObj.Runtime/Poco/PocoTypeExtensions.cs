using System.Collections.Generic;

namespace CK.Setup
{
    /// <summary>
    /// Extends <see cref="IPocoType"/>.
    /// </summary>
    public static class PocoTypeExtensions
    {
        /// <summary>
        /// Reduces a set of <see cref="IPocoType"/> to a minimal set of independent types
        /// based on extended covariance rules implemented by <see cref="IPocoType.IsSubTypeOf(IPocoType)"/>.
        /// </summary>
        /// <typeparam name="T">Actual Poco type.</typeparam>
        /// <param name="types">This types.</param>
        /// <returns>A minimal list of independent types.</returns>
        public static List<T> ComputeMinimal<T>( this IEnumerable<T> types ) where T : IPocoType
        {
            var result = new List<T>( types );
            for( int i = 0; i < result.Count; i++ )
            {
                var a = result[i];
                int j = 0;
                while( j < i )
                {
                    if( result[j].IsSubTypeOf( a ) )
                    {
                        result.RemoveAt( i-- );
                        goto skip;
                    }
                    ++j;
                }
                while( ++j < result.Count )
                {
                    if( result[j].IsSubTypeOf( a ) )
                    {
                        result.RemoveAt( i-- );
                        goto skip;
                    }
                }
                skip:;
            }
            return result;
        }

    }
}
