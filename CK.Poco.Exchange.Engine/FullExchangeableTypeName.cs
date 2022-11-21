using CK.Core;
using System.Linq.Expressions;

namespace CK.Setup
{
    /// <summary>
    /// Holds an exchangeable type name and its nullable names.
    /// </para>
    /// </summary>
    public readonly struct FullExchangeableTypeName
    {
        /// <summary>
        /// An initialized but not exchangeable value.
        /// </summary>
        public static readonly FullExchangeableTypeName Unexchangeable = new FullExchangeableTypeName( true );

        /// <summary>
        /// Gets whether this value type has been initialized: it is either a <see cref="IsExchangeable"/>
        /// type or the <see cref="Unexchangeable"/> value.
        /// </summary>
        public bool IsInitialized => NonNullable.IsInitialized;

        /// <summary>
        /// Gets whether this type is valid for exchange.
        /// Otherwise, it is excluded.
        /// </summary>
        public bool IsExchangeable => NonNullable.IsExchangeable;

        /// <summary>
        /// Gets the exchanged name of the type based on the C# type (no mapping).
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string Name => NonNullable.Name;

        /// <summary>
        /// Gets the simplified name of the type based on the C# type (no mapping).
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string SimplifiedName => NonNullable.SimplifiedName;

        /// <summary>
        /// Gets whether <see cref="SimplifiedName"/> differs from <see cref="Name"/>.
        /// </summary>
        public bool HasSimplifiedNames => NonNullable.HasSimplifiedNames;

        /// <summary>
        /// Gets the non nullable names (same interface as this one).
        /// </summary>
        public readonly ExchangeableTypeName NonNullable;

        /// <summary>
        /// Gets the nullable type names.
        /// </summary>
        public readonly ExchangeableTypeName Nullable;

        /// <summary>
        /// Initializes a new <see cref="FullExchangeableTypeName"/> from a non nullable
        /// <see cref="ExchangeableTypeName"/>.
        /// </summary>
        /// <param name="name">The non nullable name.</param>
        public FullExchangeableTypeName( in ExchangeableTypeName nonNullable )
        {
            Throw.CheckArgument( nonNullable.IsExchangeable );
            Throw.CheckArgument( !nonNullable.Name.EndsWith( '?' ) );
            NonNullable = nonNullable;
            var n = nonNullable.Name + '?';
            Nullable = nonNullable.HasSimplifiedNames
                        ? new ExchangeableTypeName( n, nonNullable.SimplifiedName + '?' )
                        : new ExchangeableTypeName( n );
        }

        /// <summary>
        /// Initializes a new <see cref="FullExchangeableTypeName"/> from a non nullable
        /// string name.
        /// </summary>
        /// <param name="name">The non nullable name.</param>
        public FullExchangeableTypeName( string nonNullableName )
        {
            NonNullable = new ExchangeableTypeName( nonNullableName );
            var n = nonNullableName + '?';
            Nullable = new ExchangeableTypeName( n );
        }

        public FullExchangeableTypeName( bool _ )
        {
            NonNullable = ExchangeableTypeName.Unexchangeable;
            Nullable = ExchangeableTypeName.Unexchangeable;
        }
    }

}
