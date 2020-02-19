using CSemVer;
using System;

namespace CK.Core
{
    /// <summary>
    /// Very simple model of a "feature": a non empty name associated to a <see cref="SVersion"/>. 
    /// </summary>
    public readonly struct VFeature : IEquatable<VFeature>, IComparable<VFeature>
    {
        /// <summary>
        /// Initializes a new <see cref="VFeature"/>.
        /// </summary>
        /// <param name="name">The feature name. Can not be null.</param>
        /// <param name="version">
        /// The version. Can not be null and must be <see cref="SVersion.IsValid"/>.
        /// If it happens to be a <see cref="CSVersion"/>, <see cref="CSVersion.ToNormalizedForm()"/> is called.
        /// </param>
        public VFeature( string name, SVersion version )
        {
            if(String.IsNullOrWhiteSpace( name ) ) throw new ArgumentException( "Must not be null or whitespace.", nameof( name ) );
            Name = name;
            if( version == null || !version.IsValid ) throw new ArgumentException( "Must be a valid SVersion.", nameof( version ) );
            Version = version.AsCSVersion?.ToNormalizedForm() ?? version;
        }


        /// <summary>
        /// Gets the name (never null except if <see cref="IsValid"/> is false).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the version that is necessarily valid (except if <see cref="IsValid"/> is false).
        /// It must be in normalized (short) form.
        /// </summary>
        public SVersion Version { get; }

        /// <summary>
        /// Gets whether this instance is valid: both <see cref="Name"/> and <see cref="Version"/> are valid.
        /// </summary>
        public bool IsValid => Name != null;

        /// <summary>
        /// Compares this instance to another: <see cref="Name"/> and descending <see cref="Version"/> are
        /// the order keys.
        /// </summary>
        /// <param name="other">The other instance to compare to. Can be invalid.</param>
        /// <returns>The negative/zero/positive standard value.</returns>
        public int CompareTo( VFeature other )
        {
            if( !IsValid )
            {
                return other.IsValid ? -1 : 0;
            }
            if( !other.IsValid ) return 1;
            int cmp = StringComparer.OrdinalIgnoreCase.Compare( Name, other.Name );
            return cmp != 0 ? cmp : other.Version.CompareTo( Version );
        }

        /// <summary>
        /// Checks equality.
        /// </summary>
        /// <param name="other">The other instance.</param>
        /// <returns>True when equals, false otherwise.</returns>
        public bool Equals( VFeature other ) => StringComparer.OrdinalIgnoreCase.Equals( Name, other.Name ) && Version == other.Version;

        /// <summary>
        /// Overridden to call <see cref="Equals(VFeature)"/>.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True when equals, false otherwise.</returns>
        public override bool Equals( object obj ) => obj is VFeature a ? Equals( a ) : false;

        /// <summary>
        /// Returns a hash based on <see cref="Name"/> and <see cref="Version"/>.
        /// </summary>
        /// <returns>The has code.</returns>
        public override int GetHashCode() => IsValid ? Version.GetHashCode() ^ Name.GetHashCode() : 0;

        /// <summary>
        /// Overridden to return <see cref="Name"/>/<see cref="Version"/> or the empty string
        /// if <see cref="IsValid"/> is false.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => IsValid ? $"{Name}/{Version}" : String.Empty;

        /// <summary>
        /// Simple parse of the "Name/Version" format that may return an invalid
        /// instance (see <see cref="IsValid"/>).
        /// This never throws.
        /// </summary>
        /// <param name="instanceName">The string to parse.</param>
        /// <returns>The resulting instance that may be invalid.</returns>
        public static VFeature TryParse( string instanceName )
        {
            int idx = instanceName.LastIndexOf( '/' );
            if( idx > 0
                && idx < instanceName.Length - 3
                && SVersion.TryParse( instanceName.Substring( idx + 1 ), out var version ) )
            {
                var n = instanceName.Substring( 0, idx );
                if( !String.IsNullOrWhiteSpace( n ) ) return new VFeature( n, version );
            }
            return new VFeature();
        }

        /// <summary>
        /// Adapts <see cref="TryParse(string)"/> to the standard pattern. This never throws.
        /// </summary>
        /// <param name="featureName">The string to parse.</param>
        /// <param name="feature">The resulting instance that may be invalid.</param>
        /// <returns>True on success, false on error.</returns>
        public static bool TryParse( string featureName, out VFeature feature )
        {
            feature = TryParse( featureName );
            return feature.IsValid;
        }

        /// <summary>
        /// Implements == operator.
        /// </summary>
        /// <param name="x">First artifact instance.</param>
        /// <param name="y">Second artifact instance.</param>
        /// <returns>True if they are equal.</returns>
        public static bool operator ==( in VFeature x, in VFeature y ) => x.Equals( y );

        /// <summary>
        /// Implements != operator.
        /// </summary>
        /// <param name="x">First artifact instance.</param>
        /// <param name="y">Second artifact instance.</param>
        /// <returns>True if they are not equal.</returns>
        public static bool operator !=( in VFeature x, in VFeature y ) => !x.Equals( y );
    }
}
