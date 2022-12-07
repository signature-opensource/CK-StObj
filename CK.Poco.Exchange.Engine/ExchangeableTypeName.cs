using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Holds an exchangeable type name for a type. This type can be not <see cref="IsExchangeable"/>:
    /// any type that structurally depends on a non exchangeable type is also not exchangeable.
    /// Any <see cref="IPocoField"/> with a non exchangeable type name should be ignored.
    /// <para>
    /// This and its <see cref="ExchangeableTypeNameBuilder"/> implements once for all a
    /// type mapping from C# type to external names with the ability to exclude some types
    /// from the exchange.
    /// </para>
    /// </summary>
    public readonly struct ExchangeableTypeName
    {
        /// <summary>
        /// An initialized but not exchangeable value.
        /// </summary>
        public static readonly ExchangeableTypeName Unexchangeable = new ExchangeableTypeName( true );

        /// <summary>
        /// Gets whether this value type has been initialized: it is either a <see cref="IsExchangeable"/>
        /// type or the <see cref="Unexchangeable"/> value.
        /// </summary>
        public bool IsInitialized => Name != null;

        /// <summary>
        /// Gets whether this type is valid for exchange.
        /// Otherwise, it is excluded.
        /// </summary>
        public bool IsExchangeable => Type != null;

        /// <summary>
        /// Gets the exchanged name of the type.
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type for this name.
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public IPocoType Type { get; }

        /// <summary>
        /// Gets whether <see cref="Name"/> differs from <see cref="IPocoType.CSharpName"/>.
        /// </summary>
        public bool HasOverriddenName => !ReferenceEquals( Name, Type.CSharpName );

        /// <summary>
        /// Initializes a new <see cref="ExchangeableTypeName"/>, optionally with a name
        /// that may not be the <see cref="IPocoType.CSharpName"/>.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="overriddenName">The optional overridden name.</param>
        public ExchangeableTypeName( IPocoType type, string? overriddenName = null )
        {
            Throw.CheckNotNullArgument( type );
            Throw.CheckArgument( overriddenName == null || !string.IsNullOrWhiteSpace( overriddenName ) );
            if( overriddenName == null
                || (!ReferenceEquals( overriddenName, type.CSharpName ) && overriddenName == type.CSharpName) )
            {
                overriddenName = type.CSharpName;
            }
            Name = overriddenName;
            Type = type;
        }

        ExchangeableTypeName( bool _ )
        {
            Name = String.Empty;
            Type = null!;
        }

        /// <summary>
        /// Overridden to return the <see cref="Name"/> or "Name / Type.<see cref="IPocoType.CSharpName"/>",
        /// "(not exchangeable)" or "(not initialized)".
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => IsInitialized
                                                ? (IsExchangeable
                                                    ? (HasOverriddenName ? $"{Name} / {Type.CSharpName}" : Name)
                                                    : "(not exchangeable)")
                                                : "(not initialized)";
    }

}
