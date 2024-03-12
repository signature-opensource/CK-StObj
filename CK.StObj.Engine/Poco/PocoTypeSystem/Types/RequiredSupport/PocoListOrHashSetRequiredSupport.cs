using CK.Core;

namespace CK.Setup
{
    sealed class PocoListOrHashSetRequiredSupport : PocoRequiredSupportType, IPocoListOrHashSetRequiredSupport
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

        public IPrimaryPocoType ItemType { get; }

        public bool IsList { get; }
    }
}
