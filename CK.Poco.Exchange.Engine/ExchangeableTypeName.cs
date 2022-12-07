using CK.Core;
using System;

namespace CK.Setup
{
    /// <summary>
    /// Holds an exchangeable type name and an optional mapped one that can be
    /// a simplified name for a type. This projection can be not <see cref="IsExchangeable"/>:
    /// any type that structurally depends on a non exchangeable type is also not exchangeable
    /// and <see cref="IPocoField"/> with a non exchangeable type name should be ignored.
    /// <para>
    /// This and its <see cref="ExchangeableTypeNameBuilder"/> implements once for all a
    /// type mapping from C# type to external names (the simplified ones being optional)
    /// with the ability to exclude some types from the exchange.
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
        public bool IsExchangeable => SimplifiedName != null;

        /// <summary>
        /// Gets the exchanged name of the type based on the C# type (no mapping).
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the simplified name of the type based on the C# type (no mapping).
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string SimplifiedName { get; }

        /// <summary>
        /// Gets whether <see cref="SimplifiedName"/> differs from <see cref="Name"/>.
        /// </summary>
        public bool HasSimplifiedNames => !ReferenceEquals( Name, SimplifiedName );

        /// <summary>
        /// Initializes a new <see cref="ExchangeableTypeName"/> with a name
        /// and a simplified name that must be different.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="simplifiedName">The simplified name.</param>
        public ExchangeableTypeName( string name, string simplifiedName )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( name );
            Throw.CheckNotNullOrWhiteSpaceArgument( simplifiedName );
            Throw.CheckArgument( name != simplifiedName );
            Name = name;
            SimplifiedName = simplifiedName;
        }

        /// <summary>
        /// Initializes a new <see cref="ExchangeableTypeName"/> with an
        /// identical name and simplified.
        /// </summary>
        /// <param name="name">The name.</param>
        public ExchangeableTypeName( string name )
        {
            Throw.CheckNotNullOrWhiteSpaceArgument( name );
            SimplifiedName = Name = name;
        }

        ExchangeableTypeName( bool _ )
        {
            Name = String.Empty;
            SimplifiedName = null!;
        }

        /// <summary>
        /// Overridden to return the <see cref="Name"/> or "Name / <see cref="SimplifiedName"/>",
        /// "(not exchangeable)" or "(not initialized)".
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => IsInitialized
                                                ? (IsExchangeable
                                                    ? (HasSimplifiedNames ? $"{Name} / {SimplifiedName}" : Name)
                                                    : "(not exchangeable)")
                                                : "(not initialized)";
    }

}
