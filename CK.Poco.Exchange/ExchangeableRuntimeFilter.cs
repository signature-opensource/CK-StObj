using System.Collections.Immutable;

namespace CK.Core
{
    /// <summary>
    /// Opaque class that provides type filtering to the serialization layer.
    /// <para>
    /// This sealed and immutable class is public to avoid call indirections but
    /// it is not intended to be used directly. Available runtime filters are
    /// exposed by the <see cref="PocoExchangeService.RuntimeFilters"/>.
    /// </para>
    /// </summary>
    public sealed class ExchangeableRuntimeFilter
    {
        readonly string _name;
        readonly ImmutableArray<int> _flags;

        /// <summary>
        /// Not to be used directly: this is initialized by generated code.
        /// </summary>
        /// <param name="name">The filter <see cref="Name"/>.</param>
        /// <param name="flags">The opaque flags used to filter types.</param>
        public ExchangeableRuntimeFilter( string name, int[] flags )
        {
            _name = name;
            _flags = ImmutableArray.Create( flags );
        }

        /// <summary>
        /// Gets this runtime type filter name.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets the opaque filter flags.
        /// </summary>
        public ImmutableArray<int> Flags => _flags;
    }
}
