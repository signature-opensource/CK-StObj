using CK.Core;

namespace CK.Setup
{
    /// <summary>
    /// Defines the "multi variance list" for a Poco.
    /// </summary>
    public sealed class PocoListOrHashSetRequiredSupport : PocoRequiredSupportType
    {
        public PocoListOrHashSetRequiredSupport( IPrimaryPocoType type, string typeName, bool isList )
            : base( typeName )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( type.IsNullable );
            Throw.CheckArgument( typeName == (isList ? $"PocoList_{type.Index}_CK" : $"PocoHashSet_{type.Index}_CK") );
            Type = type;
            IsList = isList;
        }

        /// <summary>
        /// Gets the nullable item type.
        /// </summary>
        public IPrimaryPocoType Type { get; }

        /// <summary>
        /// Gets whether this is a list (or a hash set).
        /// </summary>
        public bool IsList { get; }
    }
}
