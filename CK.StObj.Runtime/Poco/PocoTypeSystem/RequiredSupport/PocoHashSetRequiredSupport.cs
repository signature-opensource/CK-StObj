using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance hash set" for a Poco.
    /// </summary>
    public sealed class PocoHashSetRequiredSupport : PocoRequiredSupportType
    {
        public PocoHashSetRequiredSupport( IPrimaryPocoType type, string typeName )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( !type.IsNullable );
            Throw.CheckArgument( typeName == $"PocoHashSet_{type.Index}_CK" );
            Type = type;
        }

        public IPrimaryPocoType Type { get; }
    }
}
