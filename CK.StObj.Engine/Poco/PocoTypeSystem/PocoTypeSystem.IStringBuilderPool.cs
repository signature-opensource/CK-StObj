using CK.Core;
using System.Text;
using static CK.Setup.PocoTypeSystem;

namespace CK.Setup
{
    public sealed partial class PocoTypeSystem : IStringBuilderPool
    {
        /// <summary>
        /// Reusable pool of string builders.
        /// </summary>
        public interface IStringBuilderPool
        {
            /// <summary>
            /// Gets an empty ready to use builder from a basic pool: it should be used for not too big
            /// strings. This is used to compute the default values, type signatures and constructor codes
            /// (strings are not that big).
            /// </summary>
            /// <returns>An empty string builder.</returns>
            StringBuilder Get();

            /// <summary>
            /// Releases a string builder to the pool.
            /// </summary>
            /// <param name="b">The builder to release.</param>
            void Return( StringBuilder b );

            /// <summary>
            /// Releases a string builder to the pool after having returned its content.
            /// </summary>
            /// <param name="b">The builder to release.</param>
            /// <returns>The final content.</returns>
            string GetStringAndReturn( StringBuilder b );
        }

        /// <summary>
        /// Gets a pool of string builder.
        /// </summary>
        public IStringBuilderPool StringBuilderPool => this;

        StringBuilder IStringBuilderPool.Get() => _stringBuilderPool.TryPop( out var b ) ? b : new StringBuilder();

        void IStringBuilderPool.Return( StringBuilder b )
        {
            Throw.CheckNotNullArgument( b );
            b.Clear();
            _stringBuilderPool.Push( b );
        }

        string IStringBuilderPool.GetStringAndReturn( StringBuilder b )
        {
            Throw.CheckNotNullArgument( b );
            var r = b.ToString();
            b.Clear();
            _stringBuilderPool.Push( b );
            return r;
        }
    }

}
