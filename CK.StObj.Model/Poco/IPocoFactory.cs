using System;
using System.Collections.Generic;

namespace CK.Core;

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
    /// Gets the type of the final, unified, <see cref="IPocoGeneratedClass"/> class.
    /// </summary>
    Type PocoClassType { get; }

    /// <summary>
    /// Gets the primary interface that defines the Poco: this
    /// is the first entry of the <see cref="Interfaces"/> list.
    /// </summary>
    Type PrimaryInterface { get; }

    /// <summary>
    /// Gets the IPoco interface that "closes" all these <see cref="Interfaces"/>: this interface "unifies"
    /// all the other ones.
    /// If <see cref="IsClosedPoco"/> is true, then this is necessarily not null.
    /// </summary>
    Type? ClosureInterface { get; }

    /// <summary>
    /// Gets whether the <see cref="IClosedPoco"/> interface marker appear among the interfaces.
    /// When this is true, then <see cref="ClosureInterface"/> is necessarily not null.
    /// </summary>
    bool IsClosedPoco { get; }

    /// <summary>
    /// Gets the Poco name.
    /// When no [<see cref="ExternalNameAttribute"/>] is defined, this name defaults
    /// to the <see cref="CK.Core.TypeExtensions.ToCSharpName(Type?, bool, bool, bool)"/>
    /// (with the default true parameters withNamespace, typeDeclaration and useValueTupleParentheses)
    /// of the primary interface of the Poco.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the previous names of this Poco if any.
    /// These previous names can be defined by the [<see cref="ExternalNameAttribute"/>].
    /// </summary>
    IReadOnlyList<string> PreviousNames { get; }

    /// <summary>
    /// Gets all the IPoco interface types that this Poco implements.
    /// </summary>
    IReadOnlyList<Type> Interfaces { get; }
}
