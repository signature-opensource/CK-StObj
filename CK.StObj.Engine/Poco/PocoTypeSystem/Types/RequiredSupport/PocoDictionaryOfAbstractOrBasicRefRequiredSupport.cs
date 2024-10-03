using CK.Core;

namespace CK.Setup;

sealed class PocoDictionaryOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType, IPocoDictionaryOfAbstractOrBasicRefRequiredSupport
{
    public PocoDictionaryOfAbstractOrBasicRefRequiredSupport( IPocoType key, IPocoType value, string typeName )
        : base( typeName )
    {
        Throw.DebugAssert( value is IAbstractPocoType or IBasicRefPocoType );
        Throw.DebugAssert( value.IsNullable );
        Throw.DebugAssert( typeName == $"PocoDictionary_{key.Index}_{value.Index}_CK" );
        KeyType = key;
        ValueType = value;
    }

    public IPocoType KeyType { get; }

    public IPocoType ValueType { get; }
}
