using CK.Core;

namespace CK.Setup
{
    sealed class PocoHashSetOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType, IPocoHashSetOfAbstractOrBasicRefRequiredSupport
    {
        public PocoHashSetOfAbstractOrBasicRefRequiredSupport( IPocoType itemType, string typeName )
            : base( typeName )
        {
            Throw.DebugAssert( itemType is IAbstractPocoType or IBasicRefPocoType );
            Throw.DebugAssert( !itemType.IsNullable );
            Throw.DebugAssert( typeName == $"PocoHashSet_{itemType.Index}_CK" );
            ItemType = itemType;
        }

        public IPocoType ItemType { get; }
    }
}
