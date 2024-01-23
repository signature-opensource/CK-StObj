using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" dictionary for AbstractPoco values.
    /// </summary>
    public sealed class PocoDictionaryOfAbstractOrBasicRefRequiredSupport : PocoRequiredSupportType
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

        /// <summary>
        /// Gets the necessary non nullable key type.
        /// </summary>
        public IPocoType KeyType { get; }

        /// <summary>
        /// Gets the non nullable item type: a <see cref="IBasicRefPocoType"/> or <see cref="IAbstractPocoType"/>.
        /// </summary>
        public IPocoType ValueType { get; }
    }
}
