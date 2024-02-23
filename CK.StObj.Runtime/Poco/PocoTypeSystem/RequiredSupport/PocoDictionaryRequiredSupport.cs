using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance" dictionary for a Poco.
    /// </summary>
    public sealed class PocoDictionaryRequiredSupport : PocoRequiredSupportType
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

        /// <summary>
        /// Gets the necessary non nullable key type.
        /// </summary>
        public IPocoType KeyType { get; }

        /// <summary>
        /// Gets the not nullable value type.
        /// </summary>
        public IPrimaryPocoType ValueType { get; }
    }
}
