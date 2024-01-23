using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" set for Poco abstractions and BasicRefType.
    /// </summary>
    public sealed class PocoHashSetOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType
    {
        public PocoHashSetOfAbstractOrBasicRefRequiredSupport( IPocoType itemType, string typeName )
            : base( typeName )
        {
            Throw.CheckArgument( itemType is IAbstractPocoType or IBasicRefPocoType );
            Throw.CheckArgument( !itemType.IsNullable );
            Throw.CheckArgument( typeName == $"PocoHashSet_{itemType.Index}_CK" );
            ItemType = itemType;
        }

        /// <summary>
        /// Gets the non nullable item type: a <see cref="IBasicRefPocoType"/> or <see cref="IAbstractPocoType"/>.
        /// </summary>
        public IPocoType ItemType { get; }
    }
}
