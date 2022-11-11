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
            Throw.CheckArgument( !type.IsNullable );
            Throw.CheckArgument( typeName == $"PocoDictionary_{key.Index}_{type.Index}_CK" );
            Key = key;
            Type = type;
        }

        public IPocoType Key { get; }

        public IPrimaryPocoType Type { get; }
    }
}
