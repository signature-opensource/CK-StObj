using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" set for Poco abstractions.
    /// </summary>
    public sealed class PocoHashSetOfAbstractRequiredSupport : PocoRequiredSupportType
    {
        public PocoHashSetOfAbstractRequiredSupport( IAbstractPocoType itemType, string typeName )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( itemType );
            Throw.CheckArgument( !itemType.IsNullable );
            Throw.CheckArgument( typeName == $"PocoHashSet_{itemType.Index}_CK" );
            ItemType = itemType;
        }

        /// <summary>
        /// Gets the non nullable item type.
        /// </summary>
        public IAbstractPocoType ItemType { get; }
    }
}
