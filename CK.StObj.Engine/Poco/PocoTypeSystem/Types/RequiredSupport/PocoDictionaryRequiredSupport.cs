using CK.Core;

namespace CK.Setup
{
    sealed class PocoDictionaryRequiredSupport : PocoRequiredSupportType, IPocoDictionaryRequiredSupport
    {
        public PocoDictionaryRequiredSupport( IPocoType key, IPrimaryPocoType value, string typeName )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( value );
            Throw.CheckArgument( !value.IsNullable );
            Throw.CheckArgument( typeName == $"PocoDictionary_{key.Index}_{value.Index}_CK" );
            KeyType = key;
            ValueType = value;
        }

        public IPocoType KeyType { get; }

        public IPrimaryPocoType ValueType { get; }
    }
}
