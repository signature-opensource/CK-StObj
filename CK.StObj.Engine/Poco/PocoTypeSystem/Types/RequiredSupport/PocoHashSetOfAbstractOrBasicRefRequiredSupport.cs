using CK.Core;

namespace CK.Setup
{
    sealed class PocoHashSetOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType, IPocoHashSetOfAbstractOrBasicRefRequiredSupport
    {
        public PocoHashSetOfAbstractOrBasicRefRequiredSupport( IPocoType itemType, string typeName )
            : base( typeName )
        {
            Throw.CheckArgument( itemType is IAbstractPocoType or IBasicRefPocoType );
            Throw.CheckArgument( !itemType.IsNullable );
            Throw.CheckArgument( typeName == $"PocoHashSet_{itemType.Index}_CK" );
            ItemType = itemType;
        }

        public IPocoType ItemType { get; }
    }
}
