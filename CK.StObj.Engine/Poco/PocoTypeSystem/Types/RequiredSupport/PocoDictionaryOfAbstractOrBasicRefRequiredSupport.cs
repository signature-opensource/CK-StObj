using CK.Core;

namespace CK.Setup
{
    sealed class PocoDictionaryOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType, IPocoDictionaryOfAbstractOrBasicRefRequiredSupport
    {
        public PocoDictionaryOfAbstractOrBasicRefRequiredSupport( IPocoType key, IPocoType value, string typeName )
            : base( typeName )
        {
            Throw.CheckArgument( value is IAbstractPocoType or IBasicRefPocoType );
            Throw.CheckArgument( !value.IsNullable );
            Throw.CheckArgument( typeName == $"PocoDictionary_{key.Index}_{value.Index}_CK" );
            KeyType = key;
            ValueType = value;
        }

        public IPocoType KeyType { get; }

        public IPocoType ValueType { get; }
    }
}
