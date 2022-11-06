using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance list" for a Poco.
    /// </summary>
    public sealed class PocoListRequiredSupport : PocoRequiredSupportType
    {
        public PocoListRequiredSupport( IPrimaryPocoType type, string typeName )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( !type.IsNullable );
            Throw.CheckArgument( typeName == $"PocoList_{type.Index}_CK" );
            Type = type;
        }

        public IPrimaryPocoType Type { get; }
    }
}
