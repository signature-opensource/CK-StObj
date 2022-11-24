using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance dictionary" for a Poco.
    /// </summary>
    public sealed class PocoDictionaryRequiredSupport : PocoRequiredSupportType
    {
        public PocoDictionaryRequiredSupport( IPocoType key, IPrimaryPocoType type, string typeName )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( type.IsNullable );
            Throw.CheckArgument( typeName == $"PocoDictionary_{key.Index}_{type.Index}_CK" );
            Key = key;
            Type = type;
        }

        /// <summary>
        /// Gets the necessary non nullable key type.
        /// </summary>
        public IPocoType Key { get; }

        /// <summary>
        /// Gets the nullable value type.
        /// </summary>
        public IPrimaryPocoType Type { get; }
    }
}
