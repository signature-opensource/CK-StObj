using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace CK.Setup
{

    /// <summary>
    /// Captures a type nullability information.
    /// https://github.com/dotnet/roslyn/blob/master/docs/features/nullable-metadata.md
    /// </summary>
    public readonly struct NullabilityTypeInfo : IEquatable<NullabilityTypeInfo>
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

        /// <summary>
        /// Equality is based on an exact match of <see cref="Kind"/> and <see cref="NullableProfile"/>
        /// but also handles the case when one of the two is oblivious of NRT and the other is <see cref="NullablityTypeKindExtension.IsNRTFullNullable(NullabilityTypeKind)"/>.
        /// </summary>
        /// <param name="other">The other info.</param>
        /// <returns>True if this is equal to other, false otherwise.</returns>
        public bool Equals( NullabilityTypeInfo other )
        {
            Debug.Assert( Kind != other.Kind || ((_profile == null) == (other._profile == null)), "If Kind are equals then both have a profile or not." );
            // Strict equality.
            if( Kind == other.Kind
                && (_profile == null || _profile.SequenceEqual( other._profile )) )
            {
                return true;
            }
            // Basic type properties must be the same.
            if( (Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) != (other.Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)) )
            {
                return false;
            }
            // If this is a Full NRT nullable and the other is not NRT aware it is necessarily nullable since this is a ReferenceOrArray type and
            // other has the same basic type kind, it is nullable by default.
            if( Kind.IsNRTFullNullable() && !other.Kind.IsNRTAware() )
            {
                Debug.Assert( other.Kind.IsArrayOrReferenceType() && other.Kind.IsNullable() );
                return true;
            }
            // Reverse the previous check.
            if( other.Kind.IsNRTFullNullable() && !Kind.IsNRTAware() )
            {
                Debug.Assert( Kind.IsArrayOrReferenceType() && Kind.IsNullable() );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Overridden to call <see cref="Equals(NullabilityTypeInfo)"/>.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True if this is equal to other, false otherwise.</returns>
        public override bool Equals( object? obj ) => obj is NullabilityTypeInfo o ? Equals( o ) : false;

        /// <summary>
        /// Overridden to combine  <see cref="Kind"/> and <see cref="NullableProfile"/>, excluding NRT flags from kind.
        /// </summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode() => HashCode.Combine( (Kind & (~NullabilityTypeKind.NRTFullNonNullable | NullabilityTypeKind.NRTFullNullable)), _profile );
    }
}
