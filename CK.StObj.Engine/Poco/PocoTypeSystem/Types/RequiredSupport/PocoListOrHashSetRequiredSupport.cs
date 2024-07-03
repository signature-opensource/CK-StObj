using CK.Core;

namespace CK.Setup
{
    sealed class PocoListOrHashSetRequiredSupport : PocoRequiredSupportType, IPocoListOrHashSetRequiredSupport
    {
        public PocoListOrHashSetRequiredSupport( IPrimaryPocoType itemType, string typeName, bool isList )
            : base( typeName )
        {
            Throw.DebugAssert( itemType != null );
            Throw.DebugAssert( itemType.IsNullable );
            Throw.DebugAssert( typeName == (isList ? $"PocoList_{itemType.Index}_CK" : $"PocoHashSet_{itemType.Index}_CK") );
            ItemType = itemType;
            IsList = isList;
        }

        public IPrimaryPocoType ItemType { get; }

        public bool IsList { get; }
    }
}
