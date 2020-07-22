using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Setup
{
    /// <summary>
    /// Captures a type nullability information.
    /// </summary>
    public readonly struct NullabilityTypeInfo
    {
        readonly bool[]? _profile; 

        /// <summary>
        /// Gets the root <see cref="NullabilityTypeKind"/>.
        /// </summary>
        public readonly NullabilityTypeKind Kind;

        /// <summary>
        /// Gets the full nullable profile or an empty span if there is no complex NRT marker.
        /// When not empty, it starts with the nullable indicator of the root type. 
        /// </summary>
        public ReadOnlySpan<bool> NullableProfile => _profile == null ? ReadOnlySpan<bool>.Empty : _profile;

        /// <summary>
        /// Initializes a new <see cref="NullabilityTypeInfo"/>.
        /// </summary>
        /// <param name="kind">The <see cref="Kind"/>.</param>
        /// <param name="nullableProfile">The optional <see cref="NullableProfile"/>.</param>
        public NullabilityTypeInfo( NullabilityTypeKind kind, bool[]? nullableProfile )
        {
            Kind = kind;
            _profile = nullableProfile;
        }

    }
}
