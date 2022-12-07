using CK.Core;
using System.Diagnostics;
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
        /// Gets the non nullable exchanged name of the type.
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public string Name => NonNullable.Name;

        /// <summary>
        /// Gets the non nullable type.
        /// <see cref="IsExchangeable"/> must be true for this to be accessed.
        /// </summary>
        public IPocoType Type => NonNullable.Type;

        /// <summary>
        /// Gets whether <see cref="Name"/> differs from <see cref="IPocoType.CSharpName"/>.
        /// </summary>
        public bool HasOverriddenName => NonNullable.HasOverriddenName;

        /// <summary>
        /// Gets the non nullable name (same interface as this one).
        /// </summary>
        public readonly ExchangeableTypeName NonNullable;

        /// <summary>
        /// Gets the nullable type name.
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
            Throw.CheckArgument( !nonNullable.Type.IsNullable );
            Throw.CheckArgument( !nonNullable.Name.EndsWith( '?' ) );
            NonNullable = nonNullable;
            Nullable = nonNullable.HasOverriddenName
                        ? new ExchangeableTypeName( nonNullable.Type.Nullable, nonNullable.Name + '?' )
                        : new ExchangeableTypeName( nonNullable.Type.Nullable );
        }

        /// <summary>
        /// Initializes a new <see cref="FullExchangeableTypeName"/> from a non nullable
        /// <see cref="IPocoType"/> and an optional overridden name.
        /// </summary>
        /// <param name="name">The non nullable name.</param>
        public FullExchangeableTypeName( IPocoType nonNullable, string? overriddenName = null )
        {
            Throw.CheckArgument( nonNullable.IsExchangeable );
            Throw.CheckArgument( !nonNullable.IsNullable );
            Debug.Assert( !nonNullable.CSharpName.EndsWith( '?' ) );
            NonNullable = new ExchangeableTypeName( nonNullable, overriddenName );
            Nullable = NonNullable.HasOverriddenName
                        ? new ExchangeableTypeName( nonNullable.Nullable, NonNullable.Name + '?' )
                        : new ExchangeableTypeName( nonNullable.Nullable );
        }

        public FullExchangeableTypeName( bool _ )
        {
            NonNullable = ExchangeableTypeName.Unexchangeable;
            Nullable = ExchangeableTypeName.Unexchangeable;
        }

        public override string ToString() => NonNullable.ToString();
    }

}
