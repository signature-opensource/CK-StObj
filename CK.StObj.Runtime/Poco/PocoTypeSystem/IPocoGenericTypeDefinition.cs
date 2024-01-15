using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CK.Setup
{
    /// <summary>
    /// A generic definition type is not a <see cref="IPocoType"/>.
    /// It models open generics like <c>ICommand&lt;out TResult&gt;</c> and is accessible only
    /// from <see cref="IAbstractPocoType.GenerericTypeDefinition"/>.
    /// </summary>
    public interface IPocoGenericTypeDefinition
    {
        /// <summary>
        /// Gets the generic type definition.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the generic parameters.
        /// </summary>
        IReadOnlyList<IPocoGenericParameter> Parameters { get; }

        /// <summary>
        /// Gets the set of <see cref="IAbstractPocoType"/> that instantiate this
        /// generic type definition.
        /// </summary>
        IReadOnlyCollection<IAbstractPocoType> Instances { get; }

    }
}
