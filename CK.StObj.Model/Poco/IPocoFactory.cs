using System;
using System.Collections.Generic;

namespace CK.Core
{
    /// <summary>
    /// Poco factory interface: untyped base for <see cref="IPocoFactory{T}"/> objects.
    /// </summary>
    public interface IPocoFactory
    {
        /// <summary>
        /// Gets the <see cref="PocoDirectory"/> that centralizes all the factories.
        /// </summary>
        PocoDirectory PocoDirectory { get; }

        /// <summary>
        /// Creates a new Poco instance of this type.
        /// </summary>
        /// <returns>A new poco instance.</returns>
        IPoco Create();

        /// <summary>
        /// Gets the type of the final, unified, poco.
        /// </summary>
        Type PocoClassType { get; }

        /// <summary>
        /// Gets the Poco name.
        /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
        /// to the <see cref="Type.FullName"/> of the primary interface of the Poco.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the previous names of this Poco if any.
        /// </summary>
        IReadOnlyList<string> PreviousNames { get; }

        /// <summary>
        /// Gets all the IPoco interface types that this Poco implements.
        /// </summary>
        IReadOnlyList<Type> Interfaces { get; }
    }

}
