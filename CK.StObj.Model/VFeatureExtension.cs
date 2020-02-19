using CSemVer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Provides extension methods on <see cref="VFeature"/> and <see cref="IEnumerable{VFeature}"/>.
    /// </summary>
    public static class VFeatureExtensions
    {
        /// <summary>
        /// Tests whether a feature name (or a versioned "Name/SVersion") appears in these features.
        /// This never throws.
        /// </summary>
        /// <param name="this">This enumerable of features.</param>
        /// <param name="featureName">The name or the "Name/SVersion".</param>
        /// <returns>True if the feature name or versioned name exists in these features.</returns>
        public static bool Has( this IEnumerable<VFeature> @this, string featureName )
        {
            var f = VFeature.TryParse( featureName );
            return f.IsValid ? @this.Contains( f ) : @this.Any( x => x.Name == featureName );
        }

        /// <summary>
        /// Tests whether a feature appears in these features.
        /// This never throws.
        /// </summary>
        /// <param name="this">This enumerable of features.</param>
        /// <param name="f">The expected feature.</param>
        /// <returns>True if the feature exists in these features.</returns>
        public static bool Has( this IEnumerable<VFeature> @this, VFeature f ) => @this.Contains( f );

        /// <summary>
        /// Tests whether a feature appears with a minimal version in these features.
        /// This never throws.
        /// </summary>
        /// <param name="this">This enumerable of features.</param>
        /// <param name="f">The feature.</param>
        /// <returns>True if the feature exists in these features with at least the required version.</returns>
        public static bool HasAtLeast( this IEnumerable<VFeature> @this, VFeature f )
        {
            return @this.Any( x => x.Name == f.Name && x.Version >= f.Version );
        }

        /// <summary>
        /// Tests whether a feature appears with a maximal version in these features.
        /// This never throws.
        /// </summary>
        /// <param name="this">This enumerable of features.</param>
        /// <param name="f">The expected feature.</param>
        /// <returns>True if the feature exists in these features with the expected version or a lower one.</returns>
        public static bool HasAtMost( this IEnumerable<VFeature> @this, VFeature f )
        {
            return @this.Any( x => x.Name == f.Name && x.Version <= f.Version );
        }
    }
}
