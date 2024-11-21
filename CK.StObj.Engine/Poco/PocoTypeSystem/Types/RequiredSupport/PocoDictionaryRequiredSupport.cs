using CK.Core;

namespace CK.Setup;

sealed class PocoDictionaryRequiredSupport : PocoRequiredSupportType, IPocoDictionaryRequiredSupport
{
    public PocoDictionaryRequiredSupport( IPocoType key, IPrimaryPocoType value, string typeName )
        : base( typeName )
    {
        Throw.DebugAssert( value != null );
        Throw.DebugAssert( value.IsNullable );
        Throw.DebugAssert( typeName == $"PocoDictionary_{key.Index}_{value.Index}_CK" );
        KeyType = key;
        ValueType = value;
    }

    public IPocoType KeyType { get; }

    public IPrimaryPocoType ValueType { get; }
}
