using System;

namespace CK.Core
{
    /// <summary>
    /// Extends <see cref="IPoco"/> objects.
    /// </summary>
    public static class PocoExtension
    {
        /// <summary>
        /// Creates and configures a <see cref="IPoco"/> instance.
        /// </summary>
        /// <typeparam name="T">Type of the poco.</typeparam>
        /// <param name="this">This poco factory.</param>
        /// <param name="configure">Configuration action.</param>
        /// <returns>The configured instance.</returns>
        public static T Create<T>( this IPocoFactory<T> @this, Action<T> configure ) where T : IPoco
        {
            T p = @this.Create();
            configure( p );
            return p;
        }

    }
}
