using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" list or set for a Poco.
    /// </summary>
    public sealed class PocoListOrHashSetRequiredSupport : PocoRequiredSupportType
    {
        public PocoListOrHashSetRequiredSupport( IPrimaryPocoType itemType, string typeName, bool isList )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( itemType );
            Throw.CheckArgument( !itemType.IsNullable );
            Throw.CheckArgument( typeName == (isList ? $"PocoList_{itemType.Index}_CK" : $"PocoHashSet_{itemType.Index}_CK") );
            ItemType = itemType;
            IsList = isList;
        }

        /// <summary>
        /// Gets the not nullable item type.
        /// </summary>
        public IPrimaryPocoType ItemType { get; }

        /// <summary>
        /// Gets whether this is a list (or a hash set).
        /// </summary>
        public bool IsList { get; }
    }
}
